import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  FeatureFlagDto,
  FeatureFlagModuleDto,
  ToggleFeatureFlagRequest,
} from '@/src/services/wayd-api'
import { getFeatureFlagsClient } from '@/src/services/clients'

export const featureFlagsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getFeatureFlags: builder.query<FeatureFlagDto[], void>({
      queryFn: async () => {
        try {
          const data = await getFeatureFlagsClient().featureFlags()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return {
            error: error ?? new Error('Unknown error loading feature flags'),
          }
        }
      },
      providesTags: (result) =>
        result
          ? [
              QueryTags.FeatureFlag,
              ...result.map(({ id }) => ({
                type: QueryTags.FeatureFlag as const,
                id,
              })),
            ]
          : [QueryTags.FeatureFlag],
    }),
    getFeatureFlagModules: builder.query<FeatureFlagModuleDto[], void>({
      queryFn: async () => {
        try {
          const data = await getFeatureFlagsClient().modules()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return {
            error:
              error ?? new Error('Unknown error loading feature flag modules'),
          }
        }
      },
    }),
    toggleFeatureFlag: builder.mutation<void, ToggleFeatureFlagRequest>({
      queryFn: async (request) => {
        try {
          await getFeatureFlagsClient().toggle(request.id, request)
          return { data: null as unknown as void }
        } catch (error) {
          console.error('API Error:', error)
          return {
            error: error ?? new Error('Unknown error toggling feature flag'),
          }
        }
      },
      // Show the new state as soon as the request starts, so the switch does
      // not snap back until the refetch lands. Rolled back on failure.
      onQueryStarted: async (request, { dispatch, queryFulfilled }) => {
        const patch = dispatch(
          featureFlagsApi.util.updateQueryData(
            'getFeatureFlags',
            undefined,
            (draft) => {
              const flag = draft.find((f) => f.id === request.id)
              if (flag) flag.isEnabled = request.isEnabled
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patch.undo()
        }
      },
      invalidatesTags: (_result, _error, { id }) => [
        { type: QueryTags.FeatureFlag, id },
        QueryTags.FeatureFlag,
        QueryTags.ClientFeatureFlag,
      ],
    }),
  }),
})

export const {
  useGetFeatureFlagsQuery,
  useGetFeatureFlagModulesQuery,
  useToggleFeatureFlagMutation,
} = featureFlagsApi
