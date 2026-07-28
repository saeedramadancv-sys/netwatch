import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AppRole, AuthResponse, LoginRequest, StoredSession, UserInfo } from '../models/auth.models';

const STORAGE_KEY = 'netwatch.session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  /**
   * The session as a signal, so templates and guards read the same source and
   * every consumer updates automatically when tokens rotate.
   */
  private readonly session = signal<StoredSession | null>(readStoredSession());

  readonly user = computed<UserInfo | null>(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly roles = computed<AppRole[]>(() => this.session()?.user.roles ?? []);

  /** Operators and admins may change things; viewers may only look. */
  readonly canEdit = computed(() => this.hasAnyRole('Admin', 'Operator'));
  readonly isAdmin = computed(() => this.hasAnyRole('Admin'));

  get accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  get refreshToken(): string | null {
    return this.session()?.refreshToken ?? null;
  }

  hasAnyRole(...roles: AppRole[]): boolean {
    const held = this.session()?.user.roles ?? [];
    return roles.some((role) => held.includes(role));
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiBaseUrl}/api/auth/login`, request)
      .pipe(tap((response) => this.store(response)));
  }

  /**
   * Exchanges the refresh token for a new pair. The server rotates on every call,
   * so the stored refresh token must be replaced with the one returned here or the
   * next refresh will be rejected as a replay.
   */
  refresh(): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiBaseUrl}/api/auth/refresh`, {
        refreshToken: this.refreshToken ?? '',
      })
      .pipe(tap((response) => this.store(response)));
  }

  /**
   * Clears the session and tells the server to revoke the refresh token.
   *
   * Local state is cleared first and the request is fire-and-forget: the user is
   * signed out of this browser whether or not the network call succeeds.
   */
  logout(redirect = true): void {
    const token = this.refreshToken;
    this.clear();

    if (token) {
      this.http
        .post(`${environment.apiBaseUrl}/api/auth/revoke`, { refreshToken: token })
        .subscribe({ error: () => undefined });
    }

    if (redirect) {
      void this.router.navigate(['/login']);
    }
  }

  clear(): void {
    this.session.set(null);
    localStorage.removeItem(STORAGE_KEY);
  }

  private store(response: AuthResponse): void {
    const stored: StoredSession = {
      accessToken: response.accessToken,
      accessTokenExpiresAtUtc: response.accessTokenExpiresAtUtc,
      refreshToken: response.refreshToken,
      user: response.user,
    };

    this.session.set(stored);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(stored));
  }
}

/**
 * Reads a persisted session, tolerating corrupt or hand-edited storage.
 * A malformed entry signs the user out rather than crashing the application on boot.
 */
function readStoredSession(): StoredSession | null {
  const raw = localStorage.getItem(STORAGE_KEY);
  if (!raw) {
    return null;
  }

  try {
    const parsed = JSON.parse(raw) as StoredSession;
    return parsed?.accessToken && parsed?.user ? parsed : null;
  } catch {
    localStorage.removeItem(STORAGE_KEY);
    return null;
  }
}
