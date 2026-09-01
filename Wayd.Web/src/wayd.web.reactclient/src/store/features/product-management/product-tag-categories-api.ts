import { getProductTagCategoriesClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  AddProductTagRequest,
  CreateProductTagCategoryRequest,
  ObjectIdAndKey,
  ProductTagCategoryDto,
  RenameProductTagRequest,
  SetProductTagCategoryActiveRequest,
  UpdateProductTagCategoryRequest,
} from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'

/** Adds a tag to one axis. The axis owns the tag, so its id travels with the request. */
export interface AddProductTagArgs extends AddProductTagRequest {
  categoryId: string
}

/** Renames a tag on one axis. */
export interface RenameProductTagArgs extends RenameProductTagRequest {
  categoryId: string
  tagId: string
}

/** Retires a tag from new use, or puts it back. */
export interface SetProductTagActiveArgs {
  categoryId: string
  tagId: string
  isActive: boolean
}

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
    createProductTagCategory: builder.mutation<
      ObjectIdAndKey,
      CreateProductTagCategoryRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getProductTagCategoriesClient().create(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.ProductTagCategory, id: 'LIST' },
      ],
    }),
    updateProductTagCategory: builder.mutation<
      void,
      UpdateProductTagCategoryRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getProductTagCategoriesClient().update(
            request.id,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.ProductTagCategory, id: 'LIST' },
      ],
    }),
    setProductTagCategoryActive: builder.mutation<
      void,
      SetProductTagCategoryActiveRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await getProductTagCategoriesClient().setActive(
            request.id,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.ProductTagCategory, id: 'LIST' },
      ],
    }),
    deleteProductTagCategory: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await getProductTagCategoriesClient().delete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.ProductTagCategory, id: 'LIST' },
      ],
    }),
    // The tag endpoints hang off their category, and the list query is the only
    // read of either — so every one of them invalidates that single LIST tag
    // rather than a tag of its own.
    addProductTag: builder.mutation<string, AddProductTagArgs>({
      queryFn: async ({ categoryId, ...request }) => {
        try {
          const data = await getProductTagCategoriesClient().addTag(
            categoryId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.ProductTagCategory, id: 'LIST' },
      ],
    }),
    renameProductTag: builder.mutation<void, RenameProductTagArgs>({
      queryFn: async ({ categoryId, tagId, ...request }) => {
        try {
          const data = await getProductTagCategoriesClient().renameTag(
            categoryId,
            tagId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.ProductTagCategory, id: 'LIST' },
      ],
    }),
    setProductTagActive: builder.mutation<void, SetProductTagActiveArgs>({
      queryFn: async ({ categoryId, tagId, isActive }) => {
        try {
          const data = await getProductTagCategoriesClient().setTagActive(
            categoryId,
            tagId,
            { isActive },
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.ProductTagCategory, id: 'LIST' },
      ],
    }),
  }),
})

export const {
  useGetProductTagCategoriesQuery,
  useCreateProductTagCategoryMutation,
  useUpdateProductTagCategoryMutation,
  useSetProductTagCategoryActiveMutation,
  useDeleteProductTagCategoryMutation,
  useAddProductTagMutation,
  useRenameProductTagMutation,
  useSetProductTagActiveMutation,
} = productTagCategoriesApi
