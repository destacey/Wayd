import { getProfileClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import { ChangePasswordRequest, UserDetailsDto, UserPreferencesDto } from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'

// Note: permissions are no longer fetched via a separate endpoint — they're
// embedded as claims in the Wayd JWT and decoded by AuthProvider. The old
// getUserPermissions query was removed as part of PR 3.2.
export const profileApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getProfile: builder.query<UserDetailsDto, void>({
      queryFn: async () => {
        try {
          const data = await getProfileClient().get()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),
    getUserPreferences: builder.query<UserPreferencesDto, void>({
      queryFn: async () => {
        try {
          const data = await getProfileClient().getPreferences()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.UserPreferences, id: 'USER' }],
    }),
    updateUserPreferences: builder.mutation<void, UserPreferencesDto>({
      queryFn: async (preferences) => {
        try {
          await getProfileClient().updatePreferences(preferences)
          return { data: null as unknown as void }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: [{ type: QueryTags.UserPreferences, id: 'USER' }],
    }),
    changePassword: builder.mutation<void, ChangePasswordRequest>({
      queryFn: async (args) => {
        try {
          await getProfileClient().changePassword(args)
          return { data: null as unknown as void }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),
  }),
})

export const {
  useGetProfileQuery,
  useGetUserPreferencesQuery,
  useUpdateUserPreferencesMutation,
  useChangePasswordMutation,
} = profileApi
