import { HttpErrorResponse, HttpEvent, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { BehaviorSubject, Observable, catchError, filter, switchMap, take, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../services/auth.service';

/**
 * Guards against a refresh stampede.
 *
 * When several requests are in flight and the access token expires, every one of
 * them gets a 401 at once. Without coordination each would call /refresh, and
 * because the server rotates refresh tokens, the first would succeed and the rest
 * would present a retired token — which the server treats as theft and responds to
 * by revoking the entire chain, signing the user out.
 *
 * So: the first 401 performs the refresh; the others park on this subject and
 * replay once the new token arrives.
 */
let refreshInFlight = false;
const refreshedToken = new BehaviorSubject<string | null>(null);

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);

  const authed = attachToken(request, auth.accessToken);

  return next(authed).pipe(
    catchError((error: unknown) => {
      const is401 = error instanceof HttpErrorResponse && error.status === 401;

      // Auth endpoints are excluded: a 401 from /login means bad credentials, and
      // retrying it through the refresh path would loop.
      if (!is401 || isAuthEndpoint(request.url) || !auth.refreshToken) {
        return throwError(() => error);
      }

      return handleUnauthorized(request, next, auth);
    }),
  );
};

function handleUnauthorized(
  request: HttpRequest<unknown>,
  next: (req: HttpRequest<unknown>) => Observable<HttpEvent<unknown>>,
  auth: AuthService,
): Observable<HttpEvent<unknown>> {
  if (refreshInFlight) {
    return refreshedToken.pipe(
      filter((token): token is string => token !== null),
      take(1),
      switchMap((token) => next(attachToken(request, token))),
    );
  }

  refreshInFlight = true;
  refreshedToken.next(null);

  return auth.refresh().pipe(
    switchMap((response) => {
      refreshInFlight = false;
      refreshedToken.next(response.accessToken);
      return next(attachToken(request, response.accessToken));
    }),
    catchError((error: unknown) => {
      // The refresh token is gone or was revoked; there is nothing left to retry.
      refreshInFlight = false;
      auth.logout();
      return throwError(() => error);
    }),
  );
}

function attachToken(request: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  if (!token || !isApiRequest(request.url)) {
    return request;
  }

  return request.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

/**
 * Only NetWatch's own API gets the token. Attaching it to an arbitrary URL would
 * hand the bearer credential to a third party.
 */
function isApiRequest(url: string): boolean {
  return environment.apiBaseUrl ? url.startsWith(environment.apiBaseUrl) : url.startsWith('/api');
}

function isAuthEndpoint(url: string): boolean {
  return url.includes('/api/auth/login') || url.includes('/api/auth/refresh') || url.includes('/api/auth/revoke');
}
