# Microsoft Entra ID sign-in (`agents-ui/`)

## Overview

`agents-ui/` signs every visitor in automatically. There is no login button and no sign-out: the app treats "using it" and "being signed in to Entra ID" as the same thing, so a visitor with no session is redirected to Entra ID before the app ever renders, and a visitor who already has one never sees a login screen at all. Once signed in, a background service keeps the access token fresh for as long as the tab (or another tab sharing the browser) stays open, so a screen the user is looking at never fails a call just because a token expired underneath it.

This is implemented with [MSAL.js](https://learn.microsoft.com/entra/msal/javascript/browser/) (`@azure/msal-browser` 5, `@azure/msal-angular` 6) using the redirect interaction type, not popups. Read this page if you're changing anything under `agents-ui/src/app/core/auth/` or `core/config/`, deploying the UI to a new origin, or debugging a sign-in failure.

## Configuration

The app has no compiled-in environment file. Settings — the Entra ID app registration and the API's base URL — come from `public/config.json`, fetched at runtime before Angular bootstraps:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "bdeeb99b-f1e9-443f-b7ff-1426199689dc",
    "ClientId": "b40defa0-5309-45c4-82fc-cb284010cc10",
    "Audience": "b40defa0-5309-45c4-82fc-cb284010cc10/.default"
  },
  "Api": {
    "BaseUrl": "https://localhost:7237"
  }
}
```

None of these values are secret — a single-page app's client id and tenant are public by design, visible in every token request it makes. The committed file carries local development values; the deployment pipeline overwrites `dist/agents-ui/browser/config.json` per environment after the build, which is why `public/` — copied verbatim, unhashed — is where this file lives rather than inside `src/app/`.

`load-app-settings.ts` fetches it with `cache: 'no-store'`, so a stale cached copy never survives a pipeline replacement; hosting should serve it `Cache-Control: no-cache` for the same reason. `parse-app-settings.ts` then validates it: every value must be a non-blank string, `AzureAd.Instance` and `Api.BaseUrl` must be absolute `http(s)` URLs, `Instance` is normalized to end with `/` and a trailing `/` is trimmed from `BaseUrl`. A validation failure names the offending keys only — never the values, in case a value is a placeholder the pipeline failed to substitute — and `main.ts` replaces `<app-root>` with a static Bootstrap alert ("The application configuration could not be loaded.") and logs the reason to `console.error`. Angular never bootstraps in that case, because the settings configure MSAL and there's nothing to start without them.

**Why `.default` with the GUID.** The SPA and the API share one app registration — `ClientId` in `config.json` is the same id as the API's own `AzureAd:ClientId`. An application requesting a token for itself must name the resource by its GUID app identifier (`<clientId>/.default`) rather than an `api://` URI; otherwise Entra ID rejects the request with [`AADSTS90009`](https://learn.microsoft.com/answers/questions/1076516/oauth-2-0-and-azure-active-directory-error-aadsts9).

## Sign-in flow

`src/main.ts` decides between three paths before anything else runs:

1. **Is this the redirect bridge page?** If `location.pathname === '/auth'`, `main.ts` short-circuits: it never fetches `config.json` and never bootstraps Angular. It only runs the MSAL redirect bridge (below). This has to happen before MSAL or Angular touch the page, because the bridge is what hands the authentication response back to MSAL — nothing else may consume it first.
2. **Otherwise, load settings, then bootstrap.** `loadAppSettings()` resolves, then `bootstrapApplication(App, createAppConfig(settings))` runs with an app initializer (`provideAppInitializer` in `core/auth/provide-auth.ts`) that **blocks first render** on `AuthStore.signIn()` and, once an account is available, `TokenRefreshService.start(account)`.
3. **If settings fail to load**, the configuration-error alert replaces `<app-root>` instead.

`src/index.html` ships a plain "Signing in…" placeholder inside `<app-root>`, so a visitor sees that instead of a blank tab while the initializer runs.

### The redirect bridge

msal-browser 5 sends every flow that goes through the identity provider — a full-page redirect or a silent renewal in a hidden iframe — to a dedicated page that runs its **redirect bridge** before anything else does, so the authentication response reaches MSAL over a channel that survives `Cross-Origin-Opener-Policy` isolation. `redirect-bridge.ts` is that page's entire logic:

```typescript
export async function runRedirectBridge(): Promise<void> {
  try {
    const { broadcastResponseToMainFrame } = await import('@azure/msal-browser/redirect-bridge');
    await broadcastResponseToMainFrame();
  } catch {
    // Nothing to relay (a direct visit or a back-button hit); inside a frame, MSAL times out on its own.
    if (window.top === window.self) {
      location.replace('/');
    }
  }
}
```

After a full-page redirect the bridge stores the response in `sessionStorage` and navigates back to the page that started sign-in, where `handleRedirectObservable` picks it up; from a hidden iframe it posts the response to the waiting page over a `BroadcastChannel`.

The redirect URI registered with Entra ID is `<origin>/auth` (`createMsalInstance` in `provide-auth.ts`), not the app root — and there is no Angular route for it. `/auth` is reached only by Entra ID's own redirect after a `loginRedirect()`/`acquireTokenRedirect()`, or by the hidden iframe MSAL uses for a silent renewal that needs the identity provider. A direct visit or a back-button hit lands here with nothing to relay; `broadcastResponseToMainFrame()` rejects, and the top-level case sends the visitor home. See Microsoft's [redirect bridge guide](https://learn.microsoft.com/entra/msal/javascript/browser/redirect-bridge) for the mechanism itself.

### `AuthStore.signIn()`

`core/auth/auth-store.ts` is an NgRx SignalStore (`{ providedIn: 'root' }`) holding `status: 'signing-in' | 'signed-in' | 'redirecting' | 'failed'` and an `errorCode`. `signIn()` runs once, from the app initializer:

1. `handleRedirectObservable({ navigateToLoginRequestUrl: false })` — processes a pending redirect response, if the bridge just relayed one. `navigateToLoginRequestUrl` is `false` because the bridge has already returned the browser to the app; with MSAL's default (`true`), a mismatch between the stored login-request URL and the page the bridge lands on (a trailing slash, `index.html`) ends in an app that keeps reloading itself.
2. It takes the account from the redirect result, else the active account, else the first account of the configured tenant.
3. **No account at all** → `requireInteraction('login')`, which starts a `loginRedirect`.
4. A redirect error caught by `handleRedirectObservable` sets `status: 'failed'` with MSAL's own error code.

An account found by any of the three sources is set active (`setActiveAccount`) and returned; the app initializer then hands it to `TokenRefreshService.start()`.

### The loop breaker

`requireInteraction(kind)` is the **only** place an automatic redirect starts (both for a missing account and for a renewal that needs interaction — see below), which makes it the one place a redirect loop can be stopped. Before starting a `loginRedirect`/`acquireTokenRedirect`, it checks a `sessionStorage` stamp (`andes.auth.redirectStartedAt`):

- No stamp, or one older than 60 seconds → stamp the current time, set `status: 'redirecting'`, and start the redirect. This stays pending while the page navigates away; it settles (with `status: 'failed'`) only if MSAL could not navigate at all — its navigation timed out, another interaction was already in progress, or the page is inside a frame.
- A stamp younger than 60 seconds → the last redirect came back without producing a usable account or token, so redirecting again would loop. Sets `status: 'failed'` with `errorCode: 'auto_sign_in_loop'` instead.

`markSignedIn()` (called once a token is actually in hand) and `retrySignIn()` both clear the stamp — the first because sign-in succeeded, the second because the visitor explicitly asked to try again. Blocked `sessionStorage` (private browsing, a strict cookie policy) disables the loop breaker itself but not the redirect it's meant to guard.

### When sign-in fails

`app.html` renders `<router-outlet />` for every status except `'failed'`, which instead shows a Bootstrap `alert-danger` with the error code and a **Try again** button (`auth.retrySignIn()`). There is no user menu and no sign-out anywhere in the app — an intentional gap, not an oversight: this app has no notion of an anonymous, signed-out state to return to.

## Token renewal

MSAL has no built-in refresh timer — it only renews a token when something asks for one. `TokenRefreshService` (`@Service()`, `core/auth/token-refresh-service.ts`) is that something, and it starts **before first render**, as part of the same app initializer that runs `signIn()`.

### The load-time forced refresh

`start(account)` immediately calls `acquireTokenSilent` with `forceRefresh: true` and `refreshTokenExpirationOffsetSeconds: 7200`: it fetches a fresh access token, and renews the SPA's refresh token (which does not slide — it's fixed at 24 hours from the original sign-in) when under two hours of it remain. This follows MSAL's guidance on [avoiding interactive interruptions in the middle of a user's session](https://learn.microsoft.com/entra/msal/javascript/browser/token-lifetimes#avoiding-interactive-interruptions-in-the-middle-of-a-users-session): if this forced refresh needs interaction, `requireInteraction` redirects while the app is still blocked on the initializer, not mid-session.

### The renewal timer

Once `start()`'s refresh succeeds, `refresh-schedule.ts` computes the next check:

```typescript
export function refreshDelayMs(expiresOn: Date | null, now: number, jitter: number): number {
  if (expiresOn === null) {
    return unknownExpiryDelayMs;
  }
  const dueAt =
    expiresOn.getTime() - tokenRenewalOffsetSeconds * 1000 + insideWindowMs + jitter * jitterSpanMs;
  return Math.max(dueAt - now, minRefreshDelayMs);
}
```

`tokenRenewalOffsetSeconds` (300 seconds) is also set as `system.tokenRenewalOffsetSeconds` on the `PublicClientApplication` (`provide-auth.ts`): MSAL treats a cached token as expired inside that window, so a timer that wakes inside it gets a renewed token rather than the cached one. The timer fires 30 seconds inside the window, plus 0–30 seconds of random jitter so that several tabs sharing one `localStorage` cache don't all renew in the same instant. A null `expiresOn` schedules a 5-minute fallback check instead of a specific time.

Each successful renewal reschedules from the new `expiresOn`. **An `expiresOn` that didn't move forward** (MSAL served the cached token instead of actually renewing) counts as a failure and backs off — the timer never spins tightly against a token it can't actually refresh yet.

### Error handling

| Condition | Behavior |
|---|---|
| `InteractionRequiredAuthError` | Stops the timer and calls `requireInteraction('token', account)` — the same redirect path and loop breaker as a missing account at sign-in. |
| `BrowserAuthErrorCodes.noAccountError` | Stops the timer; nothing left to renew. |
| Anything else (network error, timeout) | Logs the MSAL error code only (`console.warn`), then retries with backoff: 30 seconds, doubling to a 5-minute ceiling. |

While offline, a due renewal doesn't fire; `online`, `visibilitychange` (turning visible), and `pageshow` all trigger an immediate check if one is overdue, because a background or bfcache-restored tab has its timers throttled or frozen by the browser and needs a nudge on return. Nothing is scheduled at all while `status` is `'redirecting'` or `'failed'`.

## The HTTP interceptor

`provideHttpClient(withInterceptors([msalInterceptor]))` (`app.config.ts`) attaches the access token to any request whose URL matches `MSAL_INTERCEPTOR_CONFIG.protectedResourceMap` — currently one entry, `<Api.BaseUrl>/*` → `[AzureAd.Audience]`. With `strictMatching: true` each URL component of that key is matched as an anchored pattern: a `*` in the host spans a single DNS label, and anywhere else it spans any characters. The `/*` is therefore required — `<Api.BaseUrl>` alone would match only the root path, and every other call to the API would go out with no `Authorization` header.

`msal.interceptor.ts` is a thin functional wrapper around msal-angular's own class-based `MsalInterceptor`. msal-angular ships only the class, and Angular 22.1's typings say DI-provided interceptors (`withInterceptorsFromDi`) may be phased out in a later release, so the wrapper keeps `agents-ui` on `withInterceptors`.

**Known limit, accepted:** on any silent token failure — including a transient network blip, not just an expired session — `MsalInterceptor` falls back to an interactive redirect and completes the original HTTP request without emitting a response. A flaky connection can look, from the calling code's point of view, identical to a session that actually needs re-authentication.

## Why `localStorage`

`cacheLocation: BrowserCacheLocation.LocalStorage` means a signed-in tab's tokens outlive that tab — closing it doesn't sign the user out, and a second tab opened later starts already signed in, without its own redirect round trip. MSAL encrypts the `localStorage` cache with a key held in a session cookie, so the trade-off is scoped to "any tab of this browser, until the browser itself closes." This is also why the renewal schedule has jitter: several tabs share one cache, and without spreading their renewals they'd all wake up and race to refresh the same token at once.

## Sequence: first visit through steady-state renewal

```mermaid
sequenceDiagram
    participant Tab as Browser tab
    participant Main as main.ts (/)
    participant Auth as AuthStore + TokenRefreshService
    participant Entra as Microsoft Entra ID
    participant Bridge as main.ts (/auth)

    Tab->>Main: GET / (index.html, main.js)
    Main->>Main: loadAppSettings() — fetch config.json (no-store)
    Main->>Main: bootstrapApplication(App, createAppConfig(settings))
    Main->>Auth: app initializer awaits AuthStore.signIn()
    Auth->>Auth: handleRedirectObservable() — no pending response
    Auth->>Auth: no cached or active account
    Auth->>Auth: requireInteraction('login') — stamp sessionStorage, status = redirecting
    Auth->>Entra: loginRedirect() (full-page navigation; promise stays pending)
    Entra-->>Tab: user authenticates
    Entra->>Tab: redirect to <origin>/auth#code=…
    Tab->>Bridge: GET /auth (no config fetch, no bootstrap)
    Bridge->>Bridge: broadcastResponseToMainFrame() stores the response in sessionStorage
    Bridge->>Tab: location.replace(page that started sign-in)
    Tab->>Main: GET / again
    Main->>Main: loadAppSettings() → bootstrapApplication
    Main->>Auth: AuthStore.signIn() → handleRedirectObservable(navigateToLoginRequestUrl: false)
    Auth->>Entra: redeem the authorization code
    Entra-->>Auth: tokens (cached by MSAL in localStorage)
    Main->>Auth: TokenRefreshService.start(account) — acquireTokenSilent(forceRefresh)
    Auth-->>Main: token, expiresOn
    Auth->>Auth: schedule renewal at expiresOn − 270 s (+ jitter)
    Note over Auth: later — timer fires, or visibilitychange/online/pageshow if overdue
    Auth->>Auth: acquireTokenSilent() → reschedule from the new expiresOn
```

## Known limits

- **`MsalInterceptor` redirects on any silent-token failure**, not only an actually-expired session — see [The HTTP interceptor](#the-http-interceptor) above.
- **The silent iframe has to load the app's initial bundle and run the bridge inside MSAL's 10-second `iframeBridgeTimeout`.** A `redirect_bridge_timeout` error on a cold cache (nothing cached yet, a slow connection) is the signal that the bridge page needs to get lighter, not that something is broken.
- **`localStorage` tokens outlive a closed tab** until the browser itself exits — see [Why `localStorage`](#why-localstorage). This is deliberate.
- **One refresh-token round trip happens on every page load** — the forced refresh in `TokenRefreshService.start()`.
- **Whether a second silent call is served from MSAL's cache (`fromCache: true`) isn't yet verified.** msal-common caches under the scopes Entra ID actually returned and has no special handling for a `.default` request; if it misses, every API call redeems the refresh token. The fix would be requesting `<clientId>/access_as_user` instead of `.default`.

## Prerequisites outside the code

**Entra ID app registration** (`b40defa0…`, shared with the API):

- Add `http://localhost:4200/auth` for local development, and `<origin>/auth` for every deployed origin, under **Authentication → Single-page application** — not **Web**. Registering it as a Web platform redirect URI instead produces `AADSTS9002326` the first time a real sign-in is attempted.
- The app must be able to obtain its own `access_as_user` scope: under **API permissions**, grant the app the delegated permission it exposes on itself, with admin consent if the tenant requires it — `.default` only returns permissions the app already has consent for, so a missing grant here means the token that comes back carries no `scp` claim the API's `AzureAd:Scopes` check accepts.
- The token's `ver` claim should be `2.0`, so `aud` comes back as the client-id GUID that Microsoft.Identity.Web accepts with a blank `AzureAd:Audience` on the API side (see [Configuration](../operations/configuration.md#azuread--authentication)). Set the app registration manifest's `requestedAccessTokenVersion` to `2` if it's `null` or `1`.

**API CORS**, for local development — see [Configuration](../operations/configuration.md#cors) and the [runbook](../operations/runbook.md#allow-the-angular-ui-to-call-the-api) for the exact command. The API must be reached at `https://localhost:7237`, not the `http` launch profile: `UseHttpsRedirection` runs before `UseCors`, and a browser fails a preflight that gets redirected.

## Hosting requirements

- **`/auth` must serve the SPA's `index.html`** (the same fallback every other unknown path gets) with **no** `Cross-Origin-Opener-Policy` header — a COOP header here severs the channel the redirect bridge uses to hand the response back, reintroducing the problem the bridge exists to solve. Serve it `Cache-Control: no-store`; a Content-Security-Policy in front of the app needs `frame-src`/`connect-src` to allow `login.microsoftonline.com`, and `frame-ancestors 'self'` so the page can still load inside the hidden iframe MSAL uses for a silent renewal.
- **`config.json` must be served `Cache-Control: no-cache`**, matching the app's own `cache: 'no-store'` fetch — see [Configuration](#configuration) above.

## See also

- [Angular workspace](angular-workspace.md) — the workspace this feature lives in, its layout rule, and its npm scripts.
- [Configuration](../operations/configuration.md#azuread--authentication) — the API side of the same Entra ID app registration.
- [Runbook](../operations/runbook.md#allow-the-angular-ui-to-call-the-api) — CORS and local-run setup for the API this UI calls.
- Microsoft's [redirect bridge guide](https://learn.microsoft.com/entra/msal/javascript/browser/redirect-bridge) and msal-angular's [`MsalInterceptor` documentation](https://github.com/AzureAD/microsoft-authentication-library-for-js/blob/dev/lib/msal-angular/docs/msal-interceptor.md) for the library internals this page doesn't restate.
