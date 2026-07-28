/**
 * Production settings.
 *
 * The API base is empty because the built bundle is served from the API's own
 * wwwroot: requests go to the same origin they were loaded from, which removes
 * CORS and any need to bake a hostname into the bundle at build time.
 */
export const environment = {
  production: true,
  apiBaseUrl: '',
};
