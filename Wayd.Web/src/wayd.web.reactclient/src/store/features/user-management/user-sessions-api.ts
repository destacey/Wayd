import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import { UserSessionResponse } from '@/src/services/wayd-api'
import {
  getAuthenticatedAuthClient,
  getAuthRefreshToken,
} from '@/src/services/clients'

export const userSessionsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    // The caller's own refresh token is sent so the server can mark which row is this
    // device. It never leaves for any other purpose, and the response carries no tokens.
    getMySessions: builder.query<UserSessionResponse[], void>({
      queryFn: async () => {
        try {
          const data = await getAuthenticatedAuthClient().getSessions({
            refreshToken: getAuthRefreshToken() ?? undefined,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: [{ type: QueryTags.UserSessions, id: 'LIST' }],
    }),
    revokeSession: builder.mutation<void, string>({
      queryFn: async (sessionId) => {
        try {
          await getAuthenticatedAuthClient().revokeSession(sessionId)
          // Not `undefined`: RTK Query checks for the key's presence, and an explicit
          // undefined serialises to {} and is rejected as neither data nor error.
          return { data: null as unknown as void }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: [{ type: QueryTags.UserSessions, id: 'LIST' }],
    }),
    revokeAllSessions: builder.mutation<void, void>({
      queryFn: async () => {
        try {
          await getAuthenticatedAuthClient().logoutAll()
          // Not `undefined`: RTK Query checks for the key's presence, and an explicit
          // undefined serialises to {} and is rejected as neither data nor error.
          return { data: null as unknown as void }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: [{ type: QueryTags.UserSessions, id: 'LIST' }],
    }),
  }),
})

export const {
  useGetMySessionsQuery,
  useRevokeSessionMutation,
  useRevokeAllSessionsMutation,
} = userSessionsApi
