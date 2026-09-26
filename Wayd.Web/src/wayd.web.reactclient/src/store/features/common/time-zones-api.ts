import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import { TimeZoneDto } from '@/src/services/wayd-api'
import { getTimeZonesClient } from '@/src/services/clients'

export const timeZonesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getTimeZones: builder.query<TimeZoneDto[], void>({
      queryFn: async () => {
        try {
          const data = await getTimeZonesClient().getTimeZones()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return {
            error: error ?? new Error('Unknown error loading time zones'),
          }
        }
      },
      providesTags: [QueryTags.TimeZone],
      // The list only changes with a server release.
      keepUnusedDataFor: 60 * 60,
    }),
  }),
})

export const { useGetTimeZonesQuery } = timeZonesApi
