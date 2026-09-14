/// <reference types="@angular/localize" />

import { bootstrapApplication } from '@angular/platform-browser';
import { isRedirectBridge, runRedirectBridge } from '@core/auth/redirect-bridge';
import { loadAppSettings } from '@core/config/load-app-settings';
import { App } from './app/app';
import { createAppConfig } from './app/app.config';

if (isRedirectBridge(location)) {
  void runRedirectBridge();
} else {
  loadAppSettings().then(
    (settings) =>
      bootstrapApplication(App, createAppConfig(settings)).catch((err) => console.error(err)),
    (err: unknown) => {
      console.error(err);
      showConfigurationError();
    },
  );
}

// The settings configure MSAL, so without them there is nothing for Angular to start.
function showConfigurationError(): void {
  const alert = document.createElement('div');
  alert.className = 'alert alert-danger m-4';
  alert.setAttribute('role', 'alert');
  alert.textContent = 'The application configuration could not be loaded.';
  document.querySelector('app-root')?.replaceChildren(alert);
}
