/**
 * Development settings. `ng serve` runs on :4200 while the API runs on :5080, so
 * the base URL is absolute and the API's CORS policy allows that origin.
 */
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:5080',
};
