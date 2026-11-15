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
        // An instant (`format: date-time`) is turned into a `Date` as each
        // response arrives. A calendar date (`format: date`, a NodaTime
        // LocalDate) stays its `YYYY-MM-DD` string, the `CalendarDate` of
        // src/utils/calendar-date.ts: `new Date` would read it as UTC midnight
        // and show the day before west of UTC. Orval requires a decode for the
        // transform, so `String` leaves it unchanged. (Orval drops a custom
        // type's import from a single schemas file, so it is typed `string`.)
        useDatesTransform: true,
        formatType: {
          date: { type: 'string', decode: 'String' },
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
