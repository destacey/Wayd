import { getProductTagCategoriesClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import { ProductTagCategoryDto } from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'

export const productTagCategoriesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    /**
     * The product tag catalog, each category carrying its own tag options.
     *
     * @param isActive narrows to active or inactive categories; undefined returns both. A picker
     * passes true, since an inactive category cannot be tagged against.
     */
    getProductTagCategories: builder.query<
      ProductTagCategoryDto[],
      boolean | undefined
    >({
      queryFn: async (isActive = undefined) => {
        try {
          const data =
            await getProductTagCategoriesClient().getProductTagCategories(
              isActive,
            )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.ProductTagCategory, id: 'LIST' }],
    }),
  }),
})

export const { useGetProductTagCategoriesQuery } = productTagCategoriesApi
