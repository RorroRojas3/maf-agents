import { isRedirectBridge } from './redirect-bridge';

describe('isRedirectBridge', () => {
  it('matches the auth path only', () => {
    expect(isRedirectBridge({ pathname: '/auth' })).toBe(true);
    expect(isRedirectBridge({ pathname: '/' })).toBe(false);
    expect(isRedirectBridge({ pathname: '/auth/extra' })).toBe(false);
    expect(isRedirectBridge({ pathname: '/authorize' })).toBe(false);
  });
});
