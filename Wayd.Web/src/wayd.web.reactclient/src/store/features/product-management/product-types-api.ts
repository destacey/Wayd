import { getProductTypesClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import { ProductTypeDto } from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'

export const productTypesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    /**
     * The product type catalog.
     *
     * @param isActive narrows to active or inactive types; undefined returns both. A picker passes
     * true, since an inactive type cannot be assigned to a new product.
     */
    getProductTypes: builder.query<ProductTypeDto[], boolean | undefined>({
      queryFn: async (isActive = undefined) => {
        try {
          const data = await getProductTypesClient().getProductTypes(isActive)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.ProductType, id: 'LIST' }],
    }),
  }),
})

export const { useGetProductTypesQuery } = productTypesApi
