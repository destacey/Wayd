import { getProductsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  AddProductDependencyRequest,
  ChangeProductDependencyTermsRequest,
  ChangeProductStatusRequest,
  EndProductDependencyRequest,
  ProductDependenciesDto,
  RemoveProductDependencyRequest,
  UpdateProductDependencyRequest,
  CreateProductRequest,
  ObjectIdAndKey,
  ProductDto,
  ReparentProductRequest,
  StatusOptionDto,
  StatusTransitionDto,
  StatusWorkflowDetailsDto,
  LinkProductExternallyRequest,
  RetypeProductRequest,
  UpdateProductRequest,
  PagedResponseOfActivityLogDto,
  StatusCategory,
} from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'

export interface GetProductsRequest {
  parentId?: string
  productTypeId?: string
  statusCategory?: StatusCategory[]
  tagId?: string[]
}

export interface ProductDependencyMutationArgs<TRequest> {
  /** The product that has the dependency. */
  productId: string
  dependencyId: string
  /** The product depended on, whose Activity lists the change too. */
  dependsOnProductId: string
  request: TRequest
}

/**
 * Every dependency list, and the Activity of both products — each lists a change to the link between them.
 * Activity is tagged by whatever the page fetched it with, which for a product page is its id.
 */
const dependencyChangeTags = (
  productId: string,
  dependsOnProductId: string,
) => [
  { type: QueryTags.ProductDependency, id: 'LIST' },
  { type: QueryTags.ActivityLog, id: productId },
  { type: QueryTags.ActivityLog, id: dependsOnProductId },
]

/**
 * What renaming or moving a product can change.
 *
 * Every dependency row carries both ends' names and ancestry, and is rolled up onto every ancestor of
 * either end, so a product's name or place in the tree reaches dependency lists the mutation cannot name
 * — including those of products the reader expanded on a map. A move also changes which links roll up
 * where, and which fall inside a subtree and drop out of it.
 */
export const productIdentityTags = (productId: string) => [
  { type: QueryTags.Product, id: 'LIST' },
  { type: QueryTags.Product, id: productId },
  { type: QueryTags.ProductDependency, id: 'LIST' },
]

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
      // Tag by both the resolved GUID id and the original arg (key). The detail page fetches by key,
      // while every mutation invalidates by id — without the id tag those would never refetch, and
      // the page would sit on stale data until something forced a manual refetch.
      providesTags: (result, error, arg) => [
        ...(result ? [{ type: QueryTags.Product, id: result.id }] : []),
        { type: QueryTags.Product, id: arg },
      ],
    }),
    /**
     * The statuses a product can be moved to.
     *
     * Statuses are configurable, so a picker cannot hold a fixed list — and the
     * API refuses a status belonging to another workflow. Given a product's id,
     * only the moves its workflow allows from its current status; tagged with
     * that id, so changing the product's status refreshes them.
     */
    getProductStatusOptions: builder.query<StatusOptionDto[], string | void>({
      queryFn: async (productId) => {
        try {
          const data = await getProductsClient().getStatusOptions(
            productId || undefined,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, productId) => [
        { type: QueryTags.Product, id: 'STATUS_OPTIONS' },
        ...(productId ? [{ type: QueryTags.Product, id: productId }] : []),
      ],
    }),
    /**
     * A product's status history, newest first.
     *
     * Takes the product's id rather than its key, even though the endpoint accepts either. The
     * response is a list with no id of its own, so the tag can only be the argument — and a status
     * mutation knows the id alone, so a page that fetched by key would hold a tag no mutation could
     * name and keep showing the history from before the change.
     */
    getProductStatusHistory: builder.query<StatusTransitionDto[], string>({
      queryFn: async (productId) => {
        try {
          const data = await getProductsClient().getStatusHistory(productId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.StatusHistory, id: arg },
      ],
    }),
    /**
     * The workflow the product moves through, to draw its history on. Tagged with the history too: a
     * reassignment records a transition, which is what moves the product onto another workflow.
     */
    getProductStatusWorkflow: builder.query<StatusWorkflowDetailsDto, string>({
      queryFn: async (productId) => {
        try {
          const data = await getProductsClient().getStatusWorkflow(productId)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        ...(result ? [{ type: QueryTags.StatusWorkflow, id: result.id }] : []),
        { type: QueryTags.StatusHistory, id: arg },
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
      // The list only has the rows if the run finished within the wait; one still running refreshes it
      // from its import page when it lands.
      invalidatesTags: () => [
        { type: QueryTags.Product, id: 'LIST' },
        QueryTags.ImportProcess,
      ],
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
      invalidatesTags: (result, error, arg) => productIdentityTags(arg.id),
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
      invalidatesTags: (result, error, arg) => productIdentityTags(arg.id),
    }),
    linkProductExternally: builder.mutation<
      void,
      { id: string; request: LinkProductExternallyRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getProductsClient().linkExternally(id, request)
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
        // The change this mutation just made is the newest entry in the history.
        { type: QueryTags.StatusHistory, id: arg.id },
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
        // Product counts on every tag list.
        { type: QueryTags.ProductTag, id: 'LIST' },
        { type: QueryTags.ProductTagCategory, id: 'LIST' },
      ],
    }),
    /**
     * Tags a product by a typed name: the tag on a category with that name when
     * exactly one has it, otherwise the free tag, created when none has it yet.
     * Returns the id of the tag applied.
     */
    tagProductByName: builder.mutation<string, { id: string; name: string }>({
      queryFn: async ({ id, name }) => {
        try {
          const data = await getProductsClient().tagByName(id, { name })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.Product, id: 'LIST' },
        { type: QueryTags.Product, id: arg.id },
        // A new free tag appears in the free tag list; any tag's count changes.
        { type: QueryTags.ProductTag, id: 'LIST' },
        { type: QueryTags.ProductTagCategory, id: 'LIST' },
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
        // Counts change, and a free tag nothing carries any more is deleted.
        { type: QueryTags.ProductTag, id: 'LIST' },
        { type: QueryTags.ProductTagCategory, id: 'LIST' },
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
      invalidatesTags: () => [
        { type: QueryTags.Product, id: 'LIST' },
        // Deleting a product deletes the free tags only it carried.
        { type: QueryTags.ProductTag, id: 'LIST' },
      ],
    }),

    /**
     * What a product depends on and what depends on it, rolled up across its subtree.
     *
     * Every dependency list shares one tag: a link between two products also appears, rolled up, on every
     * ancestor of either, so a mutation cannot name the lists it changes.
     */
    getProductDependencies: builder.query<
      ProductDependenciesDto,
      { idOrKey: string; includeEnded?: boolean }
    >({
      queryFn: async ({ idOrKey, includeEnded }) => {
        try {
          const data = await getProductsClient().getDependencies(
            idOrKey,
            includeEnded,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.ProductDependency, id: 'LIST' }],
    }),
    addProductDependency: builder.mutation<
      string,
      { productId: string; request: AddProductDependencyRequest }
    >({
      queryFn: async ({ productId, request }) => {
        try {
          const data = await getProductsClient().addDependency(
            productId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) =>
        dependencyChangeTags(arg.productId, arg.request.dependsOnProductId),
    }),
    updateProductDependency: builder.mutation<
      void,
      ProductDependencyMutationArgs<UpdateProductDependencyRequest>
    >({
      queryFn: async ({ productId, dependencyId, request }) => {
        try {
          const data = await getProductsClient().updateDependency(
            productId,
            dependencyId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) =>
        dependencyChangeTags(arg.productId, arg.dependsOnProductId),
    }),
    endProductDependency: builder.mutation<
      void,
      ProductDependencyMutationArgs<EndProductDependencyRequest>
    >({
      queryFn: async ({ productId, dependencyId, request }) => {
        try {
          const data = await getProductsClient().endDependency(
            productId,
            dependencyId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) =>
        dependencyChangeTags(arg.productId, arg.dependsOnProductId),
    }),
    changeProductDependencyTerms: builder.mutation<
      string,
      ProductDependencyMutationArgs<ChangeProductDependencyTermsRequest>
    >({
      queryFn: async ({ productId, dependencyId, request }) => {
        try {
          const data = await getProductsClient().changeDependencyTerms(
            productId,
            dependencyId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) =>
        dependencyChangeTags(arg.productId, arg.dependsOnProductId),
    }),
    removeProductDependency: builder.mutation<
      void,
      ProductDependencyMutationArgs<RemoveProductDependencyRequest>
    >({
      queryFn: async ({ productId, dependencyId, request }) => {
        try {
          const data = await getProductsClient().removeDependency(
            productId,
            dependencyId,
            request,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) =>
        dependencyChangeTags(arg.productId, arg.dependsOnProductId),
    }),

    getProductActivities: builder.query<
      PagedResponseOfActivityLogDto,
      { idOrKey: string | number; page?: number; pageSize?: number }
    >({
      queryFn: async ({ idOrKey, page, pageSize }) => {
        try {
          const data = await getProductsClient().getActivities(
            String(idOrKey),
            page,
            pageSize,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, { idOrKey }) => [
        { type: QueryTags.ActivityLog, id: String(idOrKey) },
      ],
    }),
  }),
})

export const {
  useGetProductsQuery,
  useGetProductQuery,
  useGetProductStatusOptionsQuery,
  useGetProductStatusHistoryQuery,
  useGetProductStatusWorkflowQuery,
  useCreateProductMutation,
  useUpdateProductMutation,
  useReparentProductMutation,
  useLinkProductExternallyMutation,
  useRetypeProductMutation,
  useChangeProductStatusMutation,
  useTagProductMutation,
  useUntagProductMutation,
  useTagProductByNameMutation,
  useDeleteProductMutation,
  useGetProductDependenciesQuery,
  useAddProductDependencyMutation,
  useUpdateProductDependencyMutation,
  useEndProductDependencyMutation,
  useChangeProductDependencyTermsMutation,
  useRemoveProductDependencyMutation,
  useGetProductActivitiesQuery,
  useLazyGetProductActivitiesQuery,
} = productsApi
