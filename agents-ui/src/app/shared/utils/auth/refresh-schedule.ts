// Shared with MSAL's system.tokenRenewalOffsetSeconds: the timer must fire inside that renewal window.
export const tokenRenewalOffsetSeconds = 300;

const insideWindowMs = 30_000;
const jitterSpanMs = 30_000;
const minRefreshDelayMs = 10_000;
const unknownExpiryDelayMs = 5 * 60_000;
const firstRetryDelayMs = 30_000;
const maxRetryDelayMs = 5 * 60_000;

// `jitter` is in [0, 1); it spreads the renewals of tabs that share one localStorage cache.
export function refreshDelayMs(expiresOn: Date | null, now: number, jitter: number): number {
  if (expiresOn === null) {
    return unknownExpiryDelayMs;
  }
  const dueAt =
    expiresOn.getTime() - tokenRenewalOffsetSeconds * 1000 + insideWindowMs + jitter * jitterSpanMs;
  return Math.max(dueAt - now, minRefreshDelayMs);
}

export function retryDelayMs(attempt: number): number {
  return Math.min(firstRetryDelayMs * 2 ** Math.max(attempt - 1, 0), maxRetryDelayMs);
}
