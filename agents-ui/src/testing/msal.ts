import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { MSAL_INSTANCE, MsalBroadcastService, MsalService } from '@azure/msal-angular';
import {
  AccountInfo,
  AuthenticationResult,
  IPublicClientApplication,
  stubbedPublicClientApplication,
} from '@azure/msal-browser';
import { provideAppSettings } from '@core/config/app-settings.token';
import { testAppSettings } from './app-settings';

type Pca = IPublicClientApplication;

// The stub's remaining members are no-ops, which is all MsalService and MsalBroadcastService need to construct.
export function createFakeMsal() {
  return {
    ...stubbedPublicClientApplication,
    initialize: vi.fn<Pca['initialize']>(() => Promise.resolve()),
    handleRedirectPromise: vi.fn<Pca['handleRedirectPromise']>(() => Promise.resolve(null)),
    getActiveAccount: vi.fn<Pca['getActiveAccount']>(() => null),
    setActiveAccount: vi.fn<Pca['setActiveAccount']>(),
    getAllAccounts: vi.fn<Pca['getAllAccounts']>(() => []),
    loginRedirect: vi.fn<Pca['loginRedirect']>(() => pending()),
    acquireTokenRedirect: vi.fn<Pca['acquireTokenRedirect']>(() => pending()),
    acquireTokenSilent: vi.fn<Pca['acquireTokenSilent']>(() => Promise.resolve(authResult())),
  } satisfies Pca;
}

export type FakeMsal = ReturnType<typeof createFakeMsal>;

export function provideAuthTesting(msal: FakeMsal): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAppSettings(testAppSettings),
    { provide: MSAL_INSTANCE, useValue: msal },
    MsalService,
    MsalBroadcastService,
  ]);
}

export function account(overrides: Partial<AccountInfo> = {}): AccountInfo {
  return {
    homeAccountId: 'home-account-id',
    environment: 'login.microsoftonline.com',
    tenantId: testAppSettings.AzureAd.TenantId,
    username: 'user@example.test',
    localAccountId: 'local-account-id',
    ...overrides,
  } as AccountInfo;
}

export function authResult(
  expiresOn: Date | null = new Date(Date.now() + 60 * 60_000),
  overrides: Partial<AuthenticationResult> = {},
): AuthenticationResult {
  return {
    account: account(),
    accessToken: 'access-token',
    scopes: [testAppSettings.AzureAd.Audience],
    fromCache: false,
    expiresOn,
    ...overrides,
  } as AuthenticationResult;
}

// A redirect that navigated away never settles.
export function pending(): Promise<void> {
  return new Promise<void>(() => undefined);
}
