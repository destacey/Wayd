import { getProfileClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  ChangePasswordRequest,
  HomeLayoutResponse,
  UpdateHomeLayoutRequest,
  UserDetailsDto,
  UserPreferencesDto,
} from '@/src/services/wayd-api'
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
    getHomeLayout: builder.query<HomeLayoutResponse, void>({
      queryFn: async () => {
        try {
          const data = await getProfileClient().getHomeLayout()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [QueryTags.HomeLayout],
    }),
    updateHomeLayout: builder.mutation<null, UpdateHomeLayoutRequest>({
      queryFn: async (request) => {
        try {
          await getProfileClient().updateHomeLayout(request)
          // The API answers 204 with no body; RTK Query rejects undefined as a result.
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // The page already shows the new layout while the drawer is open; patching
      // the cache keeps it there between the drawer closing and the refetch,
      // rather than flashing the old one. Rolled back on failure.
      onQueryStarted: async (request, { dispatch, queryFulfilled }) => {
        const patchCache = () =>
          dispatch(
            profileApi.util.updateQueryData(
              'getHomeLayout',
              undefined,
              (draft) => {
                draft.widgets = request.widgets.map((w) => ({
                  ...w,
                  settings: w.settings ?? {},
                }))
              },
            ),
          )
        const patch = patchCache()
        try {
          await queryFulfilled
          // An earlier save's refetch may have landed since the patch and put
          // the cache back to what the server held before this save.
          patchCache()
        } catch {
          patch.undo()
        }
      },
      invalidatesTags: [QueryTags.HomeLayout],
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
  useGetHomeLayoutQuery,
  useUpdateHomeLayoutMutation,
  useChangePasswordMutation,
} = profileApi
