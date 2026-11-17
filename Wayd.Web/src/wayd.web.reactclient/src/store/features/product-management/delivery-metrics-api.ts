import { deliveryMetricsGetDeliveryMetrics } from '@/src/services/api/delivery-metrics'
import { apiSlice } from '../apiSlice'
import { DeliveryMetricsDto } from '@/src/services/api/wayd.schemas'
import { QueryTags } from '../query-tags'
import { toInstant } from '@/src/utils/instant'

/**
 * ISO-8601 strings rather than `Date`s: query arguments end up in the Redux store as the cache key,
 * and a `Date` there is non-serializable — the store logs an error for every one. The client wants
 * `Date`s, so the conversion happens in the queryFn instead.
 */
export interface GetDeliveryMetricsRequest {
  from: string
  to: string
  productId?: string
}

export const deliveryMetricsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    /**
     * The delivery measures over one window.
     *
     * Tagged as a single LIST entry rather than per-window: recording a deployment outcome can move
     * any window that contains it, and the client cannot know which cached windows those are.
     */
    getDeliveryMetrics: builder.query<
      DeliveryMetricsDto,
      GetDeliveryMetricsRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await deliveryMetricsGetDeliveryMetrics({
            from: toInstant(new Date(request.from)),
            to: toInstant(new Date(request.to)),
            productId: request.productId,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.DeliveryMetrics, id: 'LIST' }],
    }),
  }),
})

export const { useGetDeliveryMetricsQuery } = deliveryMetricsApi
