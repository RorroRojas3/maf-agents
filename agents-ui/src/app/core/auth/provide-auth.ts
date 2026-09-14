import {
  DOCUMENT,
  EnvironmentProviders,
  inject,
  makeEnvironmentProviders,
  provideAppInitializer,
} from '@angular/core';
import {
  MSAL_INSTANCE,
  MSAL_INTERCEPTOR_CONFIG,
  MsalBroadcastService,
  MsalInterceptor,
  MsalInterceptorConfiguration,
  MsalService,
} from '@azure/msal-angular';
import {
  BrowserCacheLocation,
  InteractionType,
  IPublicClientApplication,
  PublicClientApplication,
} from '@azure/msal-browser';
import { AppSettings } from '@shared/models/config/app-settings.model';
import { APP_SETTINGS } from '@core/config/app-settings.token';
import { tokenRenewalOffsetSeconds } from '@shared/utils/auth/refresh-schedule';
import { AuthStore } from './auth-store';
import { redirectPath } from './redirect-bridge';
import { TokenRefreshService } from './token-refresh-service';

export function provideAuth(): EnvironmentProviders {
  return makeEnvironmentProviders([
    {
      provide: MSAL_INSTANCE,
      useFactory: () => createMsalInstance(inject(APP_SETTINGS), inject(DOCUMENT)),
    },
    {
      provide: MSAL_INTERCEPTOR_CONFIG,
      useFactory: () => createInterceptorConfig(inject(APP_SETTINGS)),
    },
    MsalService,
    MsalBroadcastService,
    MsalInterceptor,
    // Bootstrap waits on this, so nothing renders before the user is signed in or redirected away.
    provideAppInitializer(async () => {
      const store = inject(AuthStore);
      const refresher = inject(TokenRefreshService);
      const account = await store.signIn();
      if (account !== null) {
        await refresher.start(account);
      }
    }),
  ]);
}

function createMsalInstance(
  { AzureAd }: AppSettings,
  document: Document,
): IPublicClientApplication {
  return new PublicClientApplication({
    auth: {
      clientId: AzureAd.ClientId,
      authority: `${AzureAd.Instance}${AzureAd.TenantId}`,
      redirectUri: new URL(redirectPath, document.location.origin).href,
    },
    cache: { cacheLocation: BrowserCacheLocation.LocalStorage },
    system: { tokenRenewalOffsetSeconds },
  });
}

function createInterceptorConfig({ AzureAd, Api }: AppSettings): MsalInterceptorConfiguration {
  const protectedResourceMap: MsalInterceptorConfiguration['protectedResourceMap'] = new Map([
    [`${Api.BaseUrl}/*`, [AzureAd.Audience]],
  ]);
  return {
    interactionType: InteractionType.Redirect,
    protectedResourceMap,
    strictMatching: true,
  };
}
