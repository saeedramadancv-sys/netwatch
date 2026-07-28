export type AppRole = 'Admin' | 'Operator' | 'Viewer';

export interface LoginRequest {
  email: string;
  password: string;
}

export interface UserInfo {
  id: string;
  email: string;
  fullName: string;
  roles: AppRole[];
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  user: UserInfo;
}

/**
 * What the client persists between page loads.
 *
 * Tokens live in localStorage so a refresh does not sign the user out. The
 * trade-off is deliberate and documented in the README: a same-origin SPA served
 * from the API's own wwwroot could use an HttpOnly cookie instead and be immune to
 * token theft via XSS, at the cost of needing CSRF protection on every mutation.
 */
export interface StoredSession {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  user: UserInfo;
}
