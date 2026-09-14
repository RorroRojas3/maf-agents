import { refreshDelayMs, retryDelayMs, tokenRenewalOffsetSeconds } from './refresh-schedule';

describe('refreshDelayMs', () => {
  const now = Date.UTC(2026, 8, 13, 12, 0, 0);
  const expiresOn = new Date(now + 60 * 60_000);
  const renewalWindowStart = expiresOn.getTime() - tokenRenewalOffsetSeconds * 1000;

  it('fires inside the MSAL renewal window whatever the jitter', () => {
    for (const jitter of [0, 0.5, 0.999]) {
      const firesAt = now + refreshDelayMs(expiresOn, now, jitter);

      expect(firesAt).toBeGreaterThan(renewalWindowStart);
      expect(firesAt).toBeLessThan(expiresOn.getTime());
    }
  });

  it('spreads renewals across 30 seconds of jitter', () => {
    expect(refreshDelayMs(expiresOn, now, 1) - refreshDelayMs(expiresOn, now, 0)).toBe(30_000);
  });

  it('waits a short floor when the token is already due', () => {
    expect(refreshDelayMs(new Date(now - 1_000), now, 0)).toBe(10_000);
  });

  it('checks again in five minutes when the expiry is unknown', () => {
    expect(refreshDelayMs(null, now, 0)).toBe(5 * 60_000);
  });
});

describe('retryDelayMs', () => {
  it('doubles from 30 seconds and caps at five minutes', () => {
    expect([1, 2, 3, 4, 5, 10].map((attempt) => retryDelayMs(attempt))).toEqual([
      30_000, 60_000, 120_000, 240_000, 300_000, 300_000,
    ]);
  });
});
