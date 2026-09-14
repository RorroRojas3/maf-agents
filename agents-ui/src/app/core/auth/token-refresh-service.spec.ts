import { TestBed } from '@angular/core/testing';
import {
  BrowserAuthError,
  BrowserAuthErrorCodes,
  InteractionRequiredAuthError,
} from '@azure/msal-browser';
import { testAppSettings } from '@testing/app-settings';
import { account, authResult, createFakeMsal, FakeMsal, provideAuthTesting } from '@testing/msal';
import { AuthStore } from './auth-store';
import { TokenRefreshService } from './token-refresh-service';

const minute = 60_000;
const scopes = [testAppSettings.AzureAd.Audience];
const correlationId = 'correlation-id';

describe('TokenRefreshService', () => {
  let msal: FakeMsal;

  const expiringIn = (minutes: number) => authResult(new Date(Date.now() + minutes * minute));
  const flush = () => vi.advanceTimersByTimeAsync(0);

  beforeEach(() => {
    vi.useFakeTimers();
    vi.spyOn(Math, 'random').mockReturnValue(0);
    vi.spyOn(console, 'warn').mockImplementation(() => undefined);
    sessionStorage.clear();
    msal = createFakeMsal();
    TestBed.configureTestingModule({ providers: [provideAuthTesting(msal)] });
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it('renews at load with a forced refresh that also renews a refresh token near expiry', async () => {
    const signedIn = account();

    await TestBed.inject(TokenRefreshService).start(signedIn);

    expect(msal.acquireTokenSilent).toHaveBeenCalledWith({
      scopes,
      account: signedIn,
      forceRefresh: true,
      refreshTokenExpirationOffsetSeconds: 7200,
    });
    expect(TestBed.inject(AuthStore).status()).toBe('signed-in');
  });

  it('renews inside the MSAL window before expiry and reschedules from the new expiry', async () => {
    const signedIn = account();
    msal.acquireTokenSilent
      .mockResolvedValueOnce(expiringIn(60))
      .mockResolvedValueOnce(expiringIn(120))
      .mockResolvedValueOnce(expiringIn(180));
    await TestBed.inject(TokenRefreshService).start(signedIn);

    await vi.advanceTimersByTimeAsync(55 * minute + 29_000);
    expect(msal.acquireTokenSilent).toHaveBeenCalledTimes(1);

    await vi.advanceTimersByTimeAsync(1_000);
    expect(msal.acquireTokenSilent).toHaveBeenCalledTimes(2);
    expect(msal.acquireTokenSilent).toHaveBeenLastCalledWith({ scopes, account: signedIn });

    await vi.advanceTimersByTimeAsync(60 * minute);
    expect(msal.acquireTokenSilent).toHaveBeenCalledTimes(3);
  });

  it('backs off instead of spinning when the expiry does not move forward', async () => {
    msal.acquireTokenSilent.mockResolvedValue(expiringIn(60));
    await TestBed.inject(TokenRefreshService).start(account());

    await vi.advanceTimersByTimeAsync(55.5 * minute);
    expect(msal.acquireTokenSilent).toHaveBeenCalledTimes(2);

    await vi.advanceTimersByTimeAsync(29_000);
    expect(msal.acquireTokenSilent).toHaveBeenCalledTimes(2);

    await vi.advanceTimersByTimeAsync(1_000);
    expect(msal.acquireTokenSilent).toHaveBeenCalledTimes(3);
  });

  it('redirects when renewal needs interaction, and stops renewing', async () => {
    const signedIn = account();
    msal.acquireTokenSilent.mockRejectedValueOnce(
      new InteractionRequiredAuthError('login_required', correlationId),
    );

    void TestBed.inject(TokenRefreshService).start(signedIn);
    await flush();

    expect(msal.acquireTokenRedirect).toHaveBeenCalledWith({ scopes, account: signedIn });
    expect(TestBed.inject(AuthStore).status()).toBe('redirecting');
    await vi.advanceTimersByTimeAsync(24 * 60 * minute);
    expect(msal.acquireTokenSilent).toHaveBeenCalledTimes(1);
  });

  it('stops when MSAL no longer holds the account', async () => {
    msal.acquireTokenSilent.mockRejectedValueOnce(
      new BrowserAuthError(BrowserAuthErrorCodes.noAccountError, correlationId),
    );

    await TestBed.inject(TokenRefreshService).start(account());
    await vi.advanceTimersByTimeAsync(24 * 60 * minute);

    expect(msal.acquireTokenSilent).toHaveBeenCalledTimes(1);
    expect(msal.acquireTokenRedirect).not.toHaveBeenCalled();
  });

  it('retries a transient failure with a growing delay and logs only the error code', async () => {
    msal.acquireTokenSilent
      .mockRejectedValueOnce(new BrowserAuthError(BrowserAuthErrorCodes.timedOut, correlationId))
      .mockRejectedValueOnce(new BrowserAuthError(BrowserAuthErrorCodes.timedOut, correlationId));

    await TestBed.inject(TokenRefreshService).start(account());
    expect(console.warn).toHaveBeenCalledWith('Token renewal failed: timed_out');

    await vi.advanceTimersByTimeAsync(30_000);
    expect(msal.acquireTokenSilent).toHaveBeenCalledTimes(2);

    await vi.advanceTimersByTimeAsync(59_000);
    expect(msal.acquireTokenSilent).toHaveBeenCalledTimes(2);

    await vi.advanceTimersByTimeAsync(1_000);
    expect(msal.acquireTokenSilent).toHaveBeenCalledTimes(3);
  });

  it('waits for the browser to come back online before retrying', async () => {
    msal.acquireTokenSilent.mockRejectedValueOnce(
      new BrowserAuthError(BrowserAuthErrorCodes.noNetworkConnectivity, correlationId),
    );
    const onLine = vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    await TestBed.inject(TokenRefreshService).start(account());

    await vi.advanceTimersByTimeAsync(10 * minute);
    expect(msal.acquireTokenSilent).toHaveBeenCalledTimes(1);

    onLine.mockReturnValue(true);
    window.dispatchEvent(new Event('online'));
    await flush();
    expect(msal.acquireTokenSilent).toHaveBeenCalledTimes(2);
  });

  it('renews on return to the tab only once the renewal has fallen due', async () => {
    vi.spyOn(document, 'visibilityState', 'get').mockReturnValue('visible');
    msal.acquireTokenSilent.mockResolvedValueOnce(expiringIn(60));
    await TestBed.inject(TokenRefreshService).start(account());

    document.dispatchEvent(new Event('visibilitychange'));
    await flush();
    expect(msal.acquireTokenSilent).toHaveBeenCalledTimes(1);

    // A frozen tab: the clock moves on while the timer never fires.
    vi.setSystemTime(Date.now() + 56 * minute);
    document.dispatchEvent(new Event('visibilitychange'));
    await flush();
    expect(msal.acquireTokenSilent).toHaveBeenCalledTimes(2);
  });

  it('renews on a back/forward-cache restore once due', async () => {
    msal.acquireTokenSilent.mockResolvedValueOnce(expiringIn(60));
    await TestBed.inject(TokenRefreshService).start(account());

    vi.setSystemTime(Date.now() + 56 * minute);
    window.dispatchEvent(new Event('pageshow'));
    await flush();

    expect(msal.acquireTokenSilent).toHaveBeenCalledTimes(2);
  });

  it('clears its timer when the application is destroyed', async () => {
    await TestBed.inject(TokenRefreshService).start(account());

    TestBed.resetTestingModule();
    await vi.advanceTimersByTimeAsync(24 * 60 * minute);

    expect(msal.acquireTokenSilent).toHaveBeenCalledTimes(1);
  });
});
