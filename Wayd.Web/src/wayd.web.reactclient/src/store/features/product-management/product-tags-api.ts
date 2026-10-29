import { getProductTagsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  MergeProductTagRequest,
  ProductTagListItemDto,
  UpdateFreeProductTagRequest,
} from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'

/** Narrows the product tag search. Every field is optional; none returns every applicable tag. */
export interface SearchProductTagsArgs {
  /** Keeps tags whose name contains this, ignoring case. */
  search?: string
  /** True for free tags only, false for tags on categories only. */
  isFree?: boolean
  /** Keeps tags that cannot be applied: inactive ones and those on inactive categories. */
  includeInactive?: boolean
}

/** Renames a free tag. */
export interface UpdateFreeProductTagArgs extends UpdateFreeProductTagRequest {
  id: string
}

/** Merges a free tag into another tag. */
export interface MergeProductTagArgs extends MergeProductTagRequest {
  id: string
}

// Every change here can change what products show, and merging into a tag on a
// category changes that tag's product count, so each invalidates both lists.
const changedTags = [
  { type: QueryTags.ProductTag, id: 'LIST' },
  { type: QueryTags.ProductTagCategory, id: 'LIST' },
  { type: QueryTags.Product, id: 'LIST' },
  QueryTags.Product,
] as const

export const productTagsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    /** Product tags on categories and free, sorted by name, each with its product count. */
    searchProductTags: builder.query<
      ProductTagListItemDto[],
      SearchProductTagsArgs | void
    >({
      queryFn: async (args) => {
        try {
          const data = await getProductTagsClient().search(
            args?.search,
            args?.isFree,
            args?.includeInactive,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.ProductTag, id: 'LIST' }],
    }),
    updateFreeProductTag: builder.mutation<void, UpdateFreeProductTagArgs>({
      queryFn: async ({ id, ...request }) => {
        try {
          const data = await getProductTagsClient().updateFreeTag(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [...changedTags],
    }),
    mergeProductTag: builder.mutation<void, MergeProductTagArgs>({
      queryFn: async ({ id, ...request }) => {
        try {
          const data = await getProductTagsClient().merge(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [...changedTags],
    }),
    deleteFreeProductTag: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await getProductTagsClient().deleteFreeTag(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [...changedTags],
    }),
  }),
})

export const {
  useSearchProductTagsQuery,
  useUpdateFreeProductTagMutation,
  useMergeProductTagMutation,
  useDeleteFreeProductTagMutation,
} = productTagsApi
