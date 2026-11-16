import { defineConfig } from 'orval'

const mutator = (name: string) => ({
  mutator: { path: 'src/services/api-fetch.ts', name },
})

type Schema = {
  allOf?: unknown[]
  properties?: object
  additionalProperties?: unknown
  type?: string
  nullable?: boolean
  items?: Schema
}

type Parameter = { in?: string; schema?: Schema; explode?: boolean }

type Spec = {
  components?: { schemas?: Record<string, unknown> }
  paths?: Record<string, Record<string, unknown>>
}

/**
 * Removes the `{ type: 'object', additionalProperties: false }` member the API
 * adds to a derived schema's `allOf`. It declares no properties, yet Orval
 * renders it as `{ [key: string]: unknown }`, an index signature that no
 * interface satisfies, so a DTO could not be passed where its base type is
 * expected.
 */
function dropEmptyClosedAllOfMembers(spec: Spec): void {
  for (const schema of Object.values(spec.components?.schemas ?? {})) {
    const s = schema as Schema
    if (!s.allOf) continue
    s.allOf = s.allOf.filter((member) => {
      const m = member as Schema
      return !(m.additionalProperties === false && !m.properties && !m.allOf)
    })
  }
}

/**
 * Adapts each query parameter to how a URL carries it.
 *
 * - An array is exploded (`status=A&status=B`), the OpenAPI default for a
 *   query and what ASP.NET binds. Orval explodes only a parameter that says
 *   so, and otherwise joins the values with commas, which the API refuses.
 * - Nothing is nullable: a query string has no null, only a parameter left
 *   out, and Orval would send the text `null`. A C# `Guid?` or `DateTime?`
 *   parameter is published nullable, so callers pass `undefined` instead.
 */
function adaptQueryParameters(spec: Spec): void {
  for (const item of Object.values(spec.paths ?? {})) {
    for (const operation of Object.values(item)) {
      const parameters = (operation as { parameters?: Parameter[] })?.parameters
      for (const parameter of parameters ?? []) {
        if (parameter.in !== 'query' || !parameter.schema) continue
        delete parameter.schema.nullable
        if (parameter.schema.items) delete parameter.schema.items.nullable
        if (parameter.schema.type === 'array') parameter.explode ??= true
      }
    }
  }
}

function adaptSpec<T>(spec: T): T {
  dropEmptyClosedAllOfMembers(spec as Spec)
  adaptQueryParameters(spec as Spec)
  return spec
}

// Generates the typed fetch client in src/services/api from the OpenAPI
// document a Debug build of Wayd.Web.Api writes. Run `npm run generate:api`.
export default defineConfig({
  wayd: {
    input: {
      target: '../Wayd.Web.Api/wwwroot/api/v1/specification.json',
      override: { transformer: adaptSpec },
    },
    output: {
      client: 'fetch',
      mode: 'tags',
      // One file per tag, and every model in one shared wayd.schemas.ts.
      target: 'src/services/api/wayd.ts',
      clean: true,
      // No barrel: callers import the tag file they use, so a page compiles only those endpoints.
      indexFiles: false,
      // A path parameter goes through encodeURIComponent, so a value holding
      // `../` cannot climb out of its segment and send the session's token to
      // another endpoint. fetch resolves dot segments before the request goes.
      urlEncodeParameters: true,
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
