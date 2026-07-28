import { HttpErrorResponse, HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { environment } from '../../../environments/environment';
import { AuthResponse } from '../models/auth.models';
import { AuthService } from '../services/auth.service';
import { authInterceptor } from './auth.interceptor';

const API = environment.apiBaseUrl;

function session(accessToken: string, refreshToken = 'refresh-1'): AuthResponse {
  return {
    accessToken,
    accessTokenExpiresAtUtc: new Date(Date.now() + 900_000).toISOString(),
    refreshToken,
    refreshTokenExpiresAtUtc: new Date(Date.now() + 604_800_000).toISOString(),
    user: { id: 'u1', email: 'admin@netwatch.test', fullName: 'Admin', roles: ['Admin'] },
  };
}

describe('authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: AuthService;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
        provideRouter([]),
      ],
    });

    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthService);
  });

  afterEach(() => {
    backend.verify();
    localStorage.clear();
  });

  function signIn(accessToken = 'token-1'): void {
    auth.login({ email: 'admin@netwatch.test', password: 'x' }).subscribe();
    backend.expectOne(`${API}/api/auth/login`).flush(session(accessToken));
  }

  it('attaches the access token to API requests', () => {
    signIn();

    http.get(`${API}/api/devices`).subscribe();

    const request = backend.expectOne(`${API}/api/devices`);
    expect(request.request.headers.get('Authorization')).toBe('Bearer token-1');
    request.flush([]);
  });

  it('does not attach the token to a third-party URL', () => {
    // The access token is a bearer credential; sending it anywhere but this API
    // would hand it to whoever owns that host.
    signIn();

    http.get('https://example.com/data').subscribe();

    const request = backend.expectOne('https://example.com/data');
    expect(request.request.headers.has('Authorization')).toBeFalse();
    request.flush({});
  });

  it('refreshes once and replays the original request after a 401', () => {
    signIn();

    let body: unknown = null;
    http.get(`${API}/api/devices`).subscribe((response) => (body = response));

    backend.expectOne(`${API}/api/devices`).flush(null, { status: 401, statusText: 'Unauthorized' });

    const refresh = backend.expectOne(`${API}/api/auth/refresh`);
    refresh.flush(session('token-2', 'refresh-2'));

    const retried = backend.expectOne(`${API}/api/devices`);
    expect(retried.request.headers.get('Authorization')).toBe('Bearer token-2');
    retried.flush([{ id: 1 }]);

    expect(body).toEqual([{ id: 1 }]);
  });

  it('refreshes only once when several requests fail together', () => {
    // Refresh tokens rotate, so a second concurrent refresh would present a retired
    // token - which the server treats as theft and answers by revoking the whole
    // chain, signing the user out.
    signIn();

    http.get(`${API}/api/devices`).subscribe();
    http.get(`${API}/api/incidents`).subscribe();

    backend.expectOne(`${API}/api/devices`).flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne(`${API}/api/incidents`).flush(null, { status: 401, statusText: 'Unauthorized' });

    // Exactly one refresh, not two — expectOne throws on a second match, and the
    // retried requests must both carry the rotated token.
    const refresh = backend.expectOne(`${API}/api/auth/refresh`);
    refresh.flush(session('token-2', 'refresh-2'));

    const retriedDevices = backend.expectOne(`${API}/api/devices`);
    const retriedIncidents = backend.expectOne(`${API}/api/incidents`);

    expect(retriedDevices.request.headers.get('Authorization')).toBe('Bearer token-2');
    expect(retriedIncidents.request.headers.get('Authorization')).toBe('Bearer token-2');

    retriedDevices.flush([]);
    retriedIncidents.flush([]);
  });

  it('signs the user out when the refresh itself is rejected', () => {
    signIn();

    let failure: unknown = null;
    http.get(`${API}/api/devices`).subscribe({ error: (error: unknown) => (failure = error) });

    backend.expectOne(`${API}/api/devices`).flush(null, { status: 401, statusText: 'Unauthorized' });
    backend.expectOne(`${API}/api/auth/refresh`).flush(null, { status: 401, statusText: 'Unauthorized' });

    // logout() fires a best-effort revoke; it is answered so verify() stays clean.
    backend.expectOne(`${API}/api/auth/revoke`).flush(null, { status: 204, statusText: 'No Content' });

    expect(auth.isAuthenticated()).toBeFalse();
    expect(failure).toBeInstanceOf(HttpErrorResponse);
  });

  it('does not try to refresh a failed login', () => {
    // A 401 from /login means bad credentials. Routing it through the refresh path
    // would loop and hide the real error from the user.
    let failure: unknown = null;
    auth.login({ email: 'admin@netwatch.test', password: 'wrong' }).subscribe({
      error: (error: unknown) => (failure = error),
    });

    backend.expectOne(`${API}/api/auth/login`).flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(failure).toBeInstanceOf(HttpErrorResponse);
    backend.expectNone(`${API}/api/auth/refresh`);
  });
});
