import { AuthError } from '@azure/msal-browser';

export function authErrorCode(error: unknown): string {
  return error instanceof AuthError ? error.errorCode : 'unknown_error';
}
