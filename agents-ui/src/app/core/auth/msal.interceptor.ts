import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MsalInterceptor } from '@azure/msal-angular';

// msal-angular ships only a class; reaching it here avoids the DI-interceptor path Angular may phase out.
export const msalInterceptor: HttpInterceptorFn = (req, next) =>
  inject(MsalInterceptor).intercept(req, { handle: next });
