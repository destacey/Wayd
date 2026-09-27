import { getDeliveryOverviewClient } from '@/src/services/clients'
import {
  DeliveryOverviewDto,
  RecentDeliveryEventDto,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import dayjs from 'dayjs'

/**
 * `YYYY-MM-DD` strings rather than `Date`s: query arguments become the Redux cache key, and a `Date`
 * there is non-serializable — the store logs an error for every one. The generated client wants
 * `Date`s, so the conversion happens in the queryFn instead.
 *
 * Both are days in the viewer's zone, which is sent along: versions ship at moments, so the server needs
 * the zone to know which day each one fell on.
 */
export interface GetDeliveryOverviewRequest {
  from: string
  to: string
  productId?: string
}

export const deliveryOverviewApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    /**
     * Version activity over one window, for a product subtree or the whole catalog.
     *
     * Tagged against versions rather than deployments: this measures what was cut and shipped, so
     * recording a deployment cannot move it.
     */
    getDeliveryOverview: builder.query<
      DeliveryOverviewDto,
      GetDeliveryOverviewRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getDeliveryOverviewClient().getDeliveryOverview(
            // Local midnight, not `new Date('YYYY-MM-DD')`, which is UTC midnight and so the previous
            // day anywhere west of Greenwich.
            dayjs(request.from).toDate(),
            dayjs(request.to).toDate(),
            request.productId,
            Intl.DateTimeFormat().resolvedOptions().timeZone,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.Version, id: 'LIST' }],
    }),

    /**
     * What has happened to versions and packages lately.
     *
     * Tagged against versions and packages both, since either changing reorders the feed.
     */
    getRecentDeliveryEvents: builder.query<
      RecentDeliveryEventDto[],
      { take?: number; productId?: string } | undefined
    >({
      queryFn: async (request = {}) => {
        try {
          const data =
            await getDeliveryOverviewClient().getRecentDeliveryEvents(
              request.take,
              request.productId,
            )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [
        { type: QueryTags.Version, id: 'LIST' },
        { type: QueryTags.ReleasePackage, id: 'LIST' },
      ],
    }),
  }),
})

export const { useGetDeliveryOverviewQuery, useGetRecentDeliveryEventsQuery } =
  deliveryOverviewApi
