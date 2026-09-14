import { DestroyRef, DOCUMENT, inject, Service } from '@angular/core';
import { MsalService } from '@azure/msal-angular';
import {
  AccountInfo,
  AuthenticationResult,
  BrowserAuthErrorCodes,
  InteractionRequiredAuthError,
  SilentRequest,
} from '@azure/msal-browser';
import { APP_SETTINGS } from '@core/config/app-settings.token';
import { refreshDelayMs, retryDelayMs } from '@shared/utils/auth/refresh-schedule';
import { authErrorCode } from './auth-error';
import { AuthStore } from './auth-store';

// Learn's "avoid interactive interruptions": at load, renew a refresh token that has under 2 h left.
const loadRefreshTokenExpirationOffsetSeconds = 7200;

type RenewOptions = Pick<SilentRequest, 'forceRefresh' | 'refreshTokenExpirationOffsetSeconds'>;

@Service()
export class TokenRefreshService {
  private readonly msal = inject(MsalService);
  private readonly store = inject(AuthStore);
  private readonly document = inject(DOCUMENT);
  private readonly scopes = [inject(APP_SETTINGS).AzureAd.Audience];

  private account: AccountInfo | null = null;
  private expiresOn: Date | null = null;
  private dueAt = Number.POSITIVE_INFINITY;
  private attempt = 0;
  private timer: ReturnType<typeof setTimeout> | undefined;
  private listeners: AbortController | null = null;
  private inFlight: Promise<void> | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.stop());
  }

  start(account: AccountInfo): Promise<void> {
    this.account = account;
    this.listen();
    return this.renew({
      forceRefresh: true,
      refreshTokenExpirationOffsetSeconds: loadRefreshTokenExpirationOffsetSeconds,
    });
  }

  private renew(options: RenewOptions = {}): Promise<void> {
    this.inFlight ??= this.acquire(options).finally(() => (this.inFlight = null));
    return this.inFlight;
  }

  private async acquire(options: RenewOptions): Promise<void> {
    const account = this.account;
    if (account === null || this.halted()) {
      return;
    }
    this.clearTimer();
    try {
      const result = await this.msal.instance.acquireTokenSilent({
        scopes: this.scopes,
        account,
        ...options,
      });
      this.renewed(result);
    } catch (error) {
      await this.failed(error, account);
    }
  }

  private renewed(result: AuthenticationResult): void {
    const previous = this.expiresOn;
    this.expiresOn = result.expiresOn;
    this.store.markSignedIn();

    // An expiry that did not move forward means MSAL served the cached token: back off, never spin.
    if (
      previous !== null &&
      result.expiresOn !== null &&
      result.expiresOn.getTime() <= previous.getTime()
    ) {
      this.retry();
      return;
    }
    this.attempt = 0;
    this.schedule(refreshDelayMs(result.expiresOn, Date.now(), Math.random()));
  }

  private failed(error: unknown, account: AccountInfo): Promise<void> {
    if (error instanceof InteractionRequiredAuthError) {
      this.stop();
      return this.store.requireInteraction('token', account);
    }
    const code = authErrorCode(error);
    if (code === BrowserAuthErrorCodes.noAccountError) {
      this.stop();
      return Promise.resolve();
    }
    console.warn(`Token renewal failed: ${code}`);
    this.retry();
    return Promise.resolve();
  }

  private retry(): void {
    this.attempt += 1;
    this.schedule(retryDelayMs(this.attempt));
  }

  private schedule(delayMs: number): void {
    this.clearTimer();
    if (this.halted()) {
      return;
    }
    this.dueAt = Date.now() + delayMs;
    this.timer = setTimeout(() => this.wake(), delayMs);
  }

  private wake(): void {
    if (Date.now() < this.dueAt || this.document.defaultView?.navigator.onLine === false) {
      return;
    }
    void this.renew();
  }

  // Background tabs throttle or freeze timers, so a renewal that fell due meanwhile runs on return.
  private listen(): void {
    if (this.listeners !== null) {
      return;
    }
    this.listeners = new AbortController();
    const { signal } = this.listeners;
    const view = this.document.defaultView;
    this.document.addEventListener(
      'visibilitychange',
      () => {
        if (this.document.visibilityState === 'visible') {
          this.wake();
        }
      },
      { signal },
    );
    view?.addEventListener('online', () => this.wake(), { signal });
    view?.addEventListener('pageshow', () => this.wake(), { signal });
  }

  private stop(): void {
    this.clearTimer();
    this.dueAt = Number.POSITIVE_INFINITY;
    this.listeners?.abort();
    this.listeners = null;
  }

  private clearTimer(): void {
    clearTimeout(this.timer);
    this.timer = undefined;
  }

  private halted(): boolean {
    const status = this.store.status();
    return status === 'redirecting' || status === 'failed';
  }
}
