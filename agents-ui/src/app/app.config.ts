import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { AppSettings } from '@shared/models/config/app-settings.model';
import { msalInterceptor } from '@core/auth/msal.interceptor';
import { provideAuth } from '@core/auth/provide-auth';
import { provideAppSettings } from '@core/config/app-settings.token';
import { routes } from './app.routes';

export function createAppConfig(settings: AppSettings): ApplicationConfig {
  return {
    providers: [
      provideBrowserGlobalErrorListeners(),
      provideAppSettings(settings),
      provideHttpClient(withInterceptors([msalInterceptor])),
      provideRouter(routes, withComponentInputBinding()),
      provideAuth(),
    ],
  };
}
