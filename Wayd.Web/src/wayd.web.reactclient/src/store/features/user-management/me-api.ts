import { getMeClient } from '@/src/services/clients'
import {
  HomeContextResponse,
  MyTeamsSummaryResponse,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'

/** The caller's own records, from `api/me`. */
export const meApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getHomeContext: builder.query<HomeContextResponse, void>({
      queryFn: async () => {
        try {
          const data = await getMeClient().getHomeContext()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),
    getMyTeamsSummary: builder.query<MyTeamsSummaryResponse, void>({
      queryFn: async () => {
        try {
          const data = await getMeClient().getTeamsSummary()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
    }),
  }),
})

export const { useGetHomeContextQuery, useGetMyTeamsSummaryQuery } = meApi
