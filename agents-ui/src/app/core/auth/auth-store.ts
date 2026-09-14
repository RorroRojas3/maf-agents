import { DOCUMENT, inject } from '@angular/core';
import { MsalService } from '@azure/msal-angular';
import { AccountInfo, AuthenticationResult } from '@azure/msal-browser';
import {
  PartialStateUpdater,
  patchState,
  signalStore,
  withMethods,
  withState,
} from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { APP_SETTINGS } from '@core/config/app-settings.token';
import { authErrorCode } from './auth-error';

export type AuthStatus = 'signing-in' | 'signed-in' | 'redirecting' | 'failed';

interface AuthState {
  status: AuthStatus;
  errorCode: string | null;
}

export const redirectStampKey = 'andes.auth.redirectStartedAt';
export const redirectLoopErrorCode = 'auto_sign_in_loop';
const redirectLoopWindowMs = 60_000;

export const AuthStore = signalStore(
  { providedIn: 'root' },
  withState<AuthState>({ status: 'signing-in', errorCode: null }),
  withMethods(
    (
      store,
      msal = inject(MsalService),
      settings = inject(APP_SETTINGS),
      document = inject(DOCUMENT),
    ) => {
      const scopes = [settings.AzureAd.Audience];

      // The only place an automatic redirect starts. A stamp younger than the window means the last
      // redirect came back without solving anything, and redirecting again would loop.
      const requireInteraction = async (
        kind: 'login' | 'token',
        account?: AccountInfo,
      ): Promise<void> => {
        if (store.status() === 'redirecting') {
          return Promise.resolve();
        }
        const now = Date.now();
        const startedAt = readRedirectStamp(document);
        if (startedAt !== null && now - startedAt < redirectLoopWindowMs) {
          patchState(store, setFailed(redirectLoopErrorCode));
          return Promise.resolve();
        }

        writeRedirectStamp(document, now);
        patchState(store, setRedirecting());
        const redirect =
          kind === 'login'
            ? msal.instance.loginRedirect({ scopes })
            : msal.instance.acquireTokenRedirect({ scopes, account });
        // Stays pending while the page navigates away; settles only when navigation was blocked.
        try {
          return await redirect;
        } catch (error) {
          return patchState(store, setFailed(authErrorCode(error)));
        }
      };

      return {
        async signIn(): Promise<AccountInfo | null> {
          let result: AuthenticationResult | null;
          try {
            // false: the bridge already returned to the start page, and a URL mismatch would reload forever.
            result = await firstValueFrom(
              msal.handleRedirectObservable({ navigateToLoginRequestUrl: false }),
            );
          } catch (error) {
            patchState(store, setFailed(authErrorCode(error)));
            return null;
          }

          const account =
            result?.account ??
            msal.instance.getActiveAccount() ??
            msal.instance.getAllAccounts({ tenantId: settings.AzureAd.TenantId })[0] ??
            null;
          if (account === null) {
            await requireInteraction('login');
            return null;
          }
          msal.instance.setActiveAccount(account);
          return account;
        },
        requireInteraction,
        markSignedIn(): void {
          clearRedirectStamp(document);
          patchState(store, setSignedIn());
        },
        retrySignIn(): Promise<void> {
          clearRedirectStamp(document);
          return requireInteraction('login');
        },
      };
    },
  ),
);

function setRedirecting(): PartialStateUpdater<AuthState> {
  return () => ({ status: 'redirecting', errorCode: null });
}

function setSignedIn(): PartialStateUpdater<AuthState> {
  return () => ({ status: 'signed-in', errorCode: null });
}

function setFailed(errorCode: string): PartialStateUpdater<AuthState> {
  return () => ({ status: 'failed', errorCode });
}

function readRedirectStamp(document: Document): number | null {
  try {
    const value = Number(document.defaultView?.sessionStorage.getItem(redirectStampKey));
    return value > 0 ? value : null;
  } catch {
    return null;
  }
}

function writeRedirectStamp(document: Document, at: number): void {
  try {
    document.defaultView?.sessionStorage.setItem(redirectStampKey, String(at));
  } catch {
    // Blocked storage disables the loop breaker; the redirect itself still runs.
  }
}

function clearRedirectStamp(document: Document): void {
  try {
    document.defaultView?.sessionStorage.removeItem(redirectStampKey);
  } catch {
    // Blocked storage: there is no stamp to clear.
  }
}
