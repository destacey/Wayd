import { isApiResponseError } from '@/src/services/api-fetch'
import { authGetProviders } from '@/src/services/api/auth'
import { AuthProvidersResponse } from '@/src/services/api/wayd.schemas'
import { apiSlice } from '../apiSlice'

/**
 * Advertises which authentication providers are enabled on this deployment.
 * Used by the login page to render the right set of login options.
 *
 * Cheap + anonymous on the backend. Not cached aggressively — admins can
 * add/remove/disable providers at runtime via the Settings UI.
 */
export const authProvidersApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getAuthProviders: builder.query<AuthProvidersResponse, void>({
      queryFn: async () => {
        try {
          const data = await authGetProviders()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error: toSerializableApiError(error) }
        }
      },
    }),
  }),
})

export const { useGetAuthProvidersQuery } = authProvidersApi

function toSerializableApiError(error: unknown) {
  if (isApiResponseError(error)) {
    return {
      status: error.status,
      data:
        error.detail ??
        error.title ??
        'Unable to load authentication providers.',
    }
  }

  // fetch rejects with an Error only when no response arrived.
  if (error instanceof Error) {
    return { status: 'FETCH_ERROR', data: error.message }
  }

  return { status: 'CUSTOM_ERROR', data: String(error) }
}
