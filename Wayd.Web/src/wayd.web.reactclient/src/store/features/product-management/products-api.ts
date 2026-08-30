import { getProductsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  ChangeProductStatusRequest,
  CreateProductRequest,
  ObjectIdAndKey,
  ProductDto,
  ReparentProductRequest,
  RetypeProductRequest,
  UpdateProductRequest,
} from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'

export interface GetProductsRequest {
  parentId?: string
  productTypeId?: string
  statusCategory?: number[]
  tagId?: string[]
}

export const productsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getProducts: builder.query<ProductDto[], GetProductsRequest | undefined>({
      queryFn: async (request = {}) => {
        try {
          const data = await getProductsClient().getProducts(
            request.parentId,
            request.productTypeId,
            request.statusCategory,
            request.tagId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.Product, id: 'LIST' }],
    }),
    getProduct: builder.query<ProductDto, string>({
      queryFn: async (id) => {
        try {
          const data = await getProductsClient().getProduct(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.Product, id: arg },
      ],
    }),
    createProduct: builder.mutation<ObjectIdAndKey, CreateProductRequest>({
      queryFn: async (request) => {
        try {
          const data = await getProductsClient().create(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.Product, id: 'LIST' }],
    }),
    updateProduct: builder.mutation<
      void,
      { id: string; request: UpdateProductRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getProductsClient().update(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.Product, id: 'LIST' },
        { type: QueryTags.Product, id: arg.id },
      ],
    }),
    reparentProduct: builder.mutation<
      void,
      { id: string; request: ReparentProductRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getProductsClient().reparent(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // The whole list: moving a node changes the parent shown on it, and can change what a
      // parent-filtered list contains on either side of the move.
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.Product, id: 'LIST' },
        { type: QueryTags.Product, id: arg.id },
      ],
    }),
    retypeProduct: builder.mutation<
      void,
      { id: string; request: RetypeProductRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getProductsClient().retype(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.Product, id: 'LIST' },
        { type: QueryTags.Product, id: arg.id },
      ],
    }),
    changeProductStatus: builder.mutation<
      void,
      { id: string; request: ChangeProductStatusRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getProductsClient().changeStatus(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.Product, id: 'LIST' },
        { type: QueryTags.Product, id: arg.id },
      ],
    }),
    tagProduct: builder.mutation<void, { id: string; tagId: string }>({
      queryFn: async ({ id, tagId }) => {
        try {
          const data = await getProductsClient().tag(id, tagId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.Product, id: 'LIST' },
        { type: QueryTags.Product, id: arg.id },
      ],
    }),
    untagProduct: builder.mutation<void, { id: string; tagId: string }>({
      queryFn: async ({ id, tagId }) => {
        try {
          const data = await getProductsClient().untag(id, tagId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.Product, id: 'LIST' },
        { type: QueryTags.Product, id: arg.id },
      ],
    }),
    deleteProduct: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await getProductsClient().delete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.Product, id: 'LIST' }],
    }),
  }),
})

export const {
  useGetProductsQuery,
  useGetProductQuery,
  useCreateProductMutation,
  useUpdateProductMutation,
  useReparentProductMutation,
  useRetypeProductMutation,
  useChangeProductStatusMutation,
  useTagProductMutation,
  useUntagProductMutation,
  useDeleteProductMutation,
} = productsApi
