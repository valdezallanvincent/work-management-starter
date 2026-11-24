import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from '../authentication/auth.service';

const AUTH_ENDPOINTS = '/api/v1/auth/';

function withToken(request: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;
}

/**
 * Adds the bearer token to API calls and, on a 401, refreshes once and replays the request.
 * Only same-origin `/api/` URLs get the token so it is never sent to a third party.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  if (!request.url.startsWith('/api/')) {
    return next(request);
  }

  const auth = inject(AuthService);
  const router = inject(Router);
  const isAuthEndpoint = request.url.startsWith(AUTH_ENDPOINTS);

  return next(withToken(request, isAuthEndpoint ? null : auth.accessToken())).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || isAuthEndpoint) {
        return throwError(() => error);
      }

      return auth.refresh().pipe(
        switchMap((token) => {
          if (!token) {
            auth.endSession(router.url);
            return throwError(() => error);
          }
          return next(withToken(request, token));
        }),
      );
    }),
  );
};
