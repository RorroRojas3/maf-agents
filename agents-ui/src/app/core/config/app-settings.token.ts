import { EnvironmentProviders, InjectionToken, makeEnvironmentProviders } from '@angular/core';
import { AppSettings } from '@shared/models/config/app-settings.model';

export const APP_SETTINGS = new InjectionToken<AppSettings>('APP_SETTINGS');

export function provideAppSettings(settings: AppSettings): EnvironmentProviders {
  return makeEnvironmentProviders([{ provide: APP_SETTINGS, useValue: settings }]);
}
