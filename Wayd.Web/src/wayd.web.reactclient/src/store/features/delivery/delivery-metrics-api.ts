import { getDeliveryMetricsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import { DeliveryMetricsDto } from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'

export interface GetDeliveryMetricsRequest {
  from: Date
  to: Date
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
          const data = await getDeliveryMetricsClient().getDeliveryMetrics(
            request.from,
            request.to,
            request.productId,
          )
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
