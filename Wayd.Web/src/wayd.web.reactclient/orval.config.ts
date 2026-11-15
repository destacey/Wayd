import { defineConfig } from 'orval'

const mutator = (name: string) => ({
  mutator: { path: 'src/services/api-fetch.ts', name },
})

// Generates the typed fetch client in src/services/api from the OpenAPI
// document a Debug build of Wayd.Web.Api writes. Run `npm run generate:api`.
export default defineConfig({
  wayd: {
    input: {
      target: '../Wayd.Web.Api/wwwroot/api/v1/specification.json',
    },
    output: {
      client: 'fetch',
      mode: 'tags',
      // One file per tag, and every model in one shared wayd.schemas.ts.
      target: 'src/services/api/wayd.ts',
      clean: true,
      // No barrel: callers import the tag file they use, so a page compiles only those endpoints.
      indexFiles: false,
      override: {
        ...mutator('apiFetch'),
        fetch: {
          // Return the body itself, as callers expect; a failed status throws.
          includeHttpResponseReturnType: false,
        },
        // The session pipeline's refresh and 401 sign-out must stay off these:
        // they send no token, or (logout) must send the stored one untouched.
        operations: {
          Auth_Login: mutator('anonymousApiFetch'),
          Auth_RefreshToken: mutator('anonymousApiFetch'),
          Auth_Exchange: mutator('anonymousApiFetch'),
          Auth_GetProviders: mutator('anonymousApiFetch'),
          Auth_Setup: mutator('anonymousApiFetch'),
          Auth_Logout: mutator('logoutApiFetch'),
        },
      },
    },
  },
})
