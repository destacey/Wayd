import { workTypeTiersGetList } from '@/src/services/api/work-type-tiers'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import { WorkTypeTierDto } from '@/src/services/api/wayd.schemas'

export const workTypeTierApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getWorkTypeTiers: builder.query<WorkTypeTierDto[], null>({
      queryFn: async () => {
        try {
          const data = await workTypeTiersGetList()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result) => [
        QueryTags.WorkTypeTier,
        ...(result?.map(({ id }) => ({ type: QueryTags.WorkTypeTier, id })) ??
          []),
      ],
    }),
  }),
  overrideExisting: false,
})

export const { useGetWorkTypeTiersQuery } = workTypeTierApi
