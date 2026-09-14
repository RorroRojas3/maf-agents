import { TestBed } from '@angular/core/testing';
import { BrowserAuthError, BrowserAuthErrorCodes, ServerError } from '@azure/msal-browser';
import { testAppSettings } from '@testing/app-settings';
import { account, authResult, createFakeMsal, FakeMsal, provideAuthTesting } from '@testing/msal';
import { AuthStore, redirectLoopErrorCode, redirectStampKey } from './auth-store';

describe('AuthStore', () => {
  const scopes = [testAppSettings.AzureAd.Audience];
  const correlationId = 'correlation-id';
  let msal: FakeMsal;

  beforeEach(() => {
    sessionStorage.clear();
    msal = createFakeMsal();
    TestBed.configureTestingModule({ providers: [provideAuthTesting(msal)] });
  });

  it('activates the account a returning redirect brings back', async () => {
    const redirected = account({ username: 'redirected@example.test' });
    msal.handleRedirectPromise.mockResolvedValueOnce(
      authResult(undefined, { account: redirected }),
    );
    const store = TestBed.inject(AuthStore);

    await expect(store.signIn()).resolves.toBe(redirected);

    expect(msal.handleRedirectPromise).toHaveBeenCalledWith(
      expect.objectContaining({ navigateToLoginRequestUrl: false }),
    );
    expect(msal.setActiveAccount).toHaveBeenCalledWith(redirected);
    expect(msal.loginRedirect).not.toHaveBeenCalled();
  });

  it('falls back to a cached account in the configured tenant', async () => {
    const cached = account();
    msal.getAllAccounts.mockReturnValue([cached]);
    const store = TestBed.inject(AuthStore);

    await expect(store.signIn()).resolves.toBe(cached);

    expect(msal.getAllAccounts).toHaveBeenCalledWith({
      tenantId: testAppSettings.AzureAd.TenantId,
    });
    expect(msal.setActiveAccount).toHaveBeenCalledWith(cached);
  });

  it('redirects to sign in when there is no account, and never lets the app start', async () => {
    const store = TestBed.inject(AuthStore);
    let settled = false;

    void store.signIn().finally(() => {
      settled = true;
    });
    await vi.waitFor(() => expect(msal.loginRedirect).toHaveBeenCalledWith({ scopes }));
    await new Promise((resolve) => setTimeout(resolve));

    expect(settled).toBe(false);
    expect(store.status()).toBe('redirecting');
    expect(sessionStorage.getItem(redirectStampKey)).not.toBeNull();
  });

  it('ignores another interaction request while a redirect is under way', async () => {
    const store = TestBed.inject(AuthStore);

    void store.requireInteraction('login');
    await store.requireInteraction('token', account());

    expect(msal.loginRedirect).toHaveBeenCalledTimes(1);
    expect(msal.acquireTokenRedirect).not.toHaveBeenCalled();
  });

  it('fails instead of looping when the last redirect came back unresolved within a minute', async () => {
    sessionStorage.setItem(redirectStampKey, String(Date.now() - 20_000));
    const store = TestBed.inject(AuthStore);

    await expect(store.signIn()).resolves.toBeNull();

    expect(msal.loginRedirect).not.toHaveBeenCalled();
    expect(store.status()).toBe('failed');
    expect(store.errorCode()).toBe(redirectLoopErrorCode);
  });

  it('redirects again once the last redirect is more than a minute old', async () => {
    sessionStorage.setItem(redirectStampKey, String(Date.now() - 61_000));
    const store = TestBed.inject(AuthStore);

    void store.signIn();

    await vi.waitFor(() => expect(msal.loginRedirect).toHaveBeenCalledTimes(1));
  });

  it('fails with the code of a redirect the browser could not perform', async () => {
    msal.loginRedirect.mockRejectedValueOnce(
      new BrowserAuthError(BrowserAuthErrorCodes.timedOut, correlationId),
    );
    const store = TestBed.inject(AuthStore);

    await expect(store.signIn()).resolves.toBeNull();

    expect(store.status()).toBe('failed');
    expect(store.errorCode()).toBe(BrowserAuthErrorCodes.timedOut);
  });

  it('fails with the code of an error the redirect returned', async () => {
    msal.handleRedirectPromise.mockRejectedValueOnce(
      new ServerError('access_denied', correlationId),
    );
    const store = TestBed.inject(AuthStore);

    await expect(store.signIn()).resolves.toBeNull();

    expect(store.status()).toBe('failed');
    expect(store.errorCode()).toBe('access_denied');
    expect(msal.loginRedirect).not.toHaveBeenCalled();
  });

  it('marks sign-in complete and clears the loop stamp', () => {
    sessionStorage.setItem(redirectStampKey, String(Date.now()));
    const store = TestBed.inject(AuthStore);

    store.markSignedIn();

    expect(store.status()).toBe('signed-in');
    expect(sessionStorage.getItem(redirectStampKey)).toBeNull();
  });

  it('lets the user retry a failed sign-in even inside the loop window', async () => {
    sessionStorage.setItem(redirectStampKey, String(Date.now()));
    const store = TestBed.inject(AuthStore);
    await store.signIn();
    expect(store.status()).toBe('failed');

    void store.retrySignIn();

    expect(msal.loginRedirect).toHaveBeenCalledWith({ scopes });
    expect(store.status()).toBe('redirecting');
  });
});
