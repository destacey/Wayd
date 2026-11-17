import {
  releasePackagesAssemble,
  releasePackagesCorrectDates,
  releasePackagesDelete,
  releasePackagesGetActivities,
  releasePackagesGetReleasePackage,
  releasePackagesGetReleasePackages,
  releasePackagesGetStatusHistory,
  releasePackagesGetStatusWorkflow,
  releasePackagesGetWorkflowActions,
  releasePackagesMarkReleased,
  releasePackagesSetManifest,
  releasePackagesWithdraw,
} from '@/src/services/api/release-packages'
import { apiSlice } from '../apiSlice'
import {
  AssembleReleasePackageRequest,
  CorrectReleasePackageDatesRequest,
  DeliveryWorkflowActionDto,
  MarkReleasePackageReleasedRequest,
  ObjectIdAndKey,
  ReleasePackageDto,
  SetReleasePackageManifestRequest,
  StatusTransitionDto,
  StatusWorkflowDetailsDto,
  WithdrawReleasePackageRequest,
  PagedResponseOfActivityLogDto,
  StatusCategory,
} from '@/src/services/api/wayd.schemas'
import { QueryTags } from '../query-tags'

export interface GetReleasePackagesRequest {
  statusCategory?: StatusCategory[]
  /** Packages naming this product in any version — what a component has ever shipped in. */
  containingProductId?: string
  /**
   * Packages naming this exact release. Narrower than `containingProductId`, and what a release's own
   * page needs: the product-wide filter would list packages that release was never part of.
   */
  containingVersionId?: string
}

/**
 * Tags every query and mutation touches for one package.
 *
 * `cacheKey` is the package's short key, following the convention the app's other slices use, and is
 * required: a detail page queries its history by that key while a mutation holds only the id, so
 * invalidating one alone leaves the history stale.
 */
export const packageTags = (id: string, cacheKey: number) => [
  { type: QueryTags.ReleasePackage, id: 'LIST' },
  { type: QueryTags.ReleasePackage, id },
  { type: QueryTags.StatusHistory, id },
  { type: QueryTags.StatusHistory, id: String(cacheKey) },
]

export const releasePackagesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    /**
     * Packages, newest first.
     *
     * Both containing-* filters read the manifest, since a release records no package of its own:
     * `containingVersionId` matches the one version (what a version's page asks), while
     * `containingProductId` matches any manifest line for that product, changed or carried forward.
     */
    getReleasePackages: builder.query<
      ReleasePackageDto[],
      GetReleasePackagesRequest | undefined
    >({
      queryFn: async (request = {}) => {
        try {
          const data = await releasePackagesGetReleasePackages({
            statusCategory: request.statusCategory,
            containingProductId: request.containingProductId,
            containingVersionId: request.containingVersionId,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.ReleasePackage, id: 'LIST' }],
    }),
    getReleasePackage: builder.query<ReleasePackageDto, string>({
      queryFn: async (id) => {
        try {
          const data = await releasePackagesGetReleasePackage(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.ReleasePackage, id: arg },
      ],
    }),
    getReleasePackageStatusHistory: builder.query<
      StatusTransitionDto[],
      string
    >({
      queryFn: async (id) => {
        try {
          const data = await releasePackagesGetStatusHistory(id)
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
     * The workflow the package moves through, to draw its history on. Tagged with the history too: a
     * reassignment records a transition, which is what moves the package onto another workflow.
     */
    getReleasePackageStatusWorkflow: builder.query<
      StatusWorkflowDetailsDto,
      string
    >({
      queryFn: async (idOrKey) => {
        try {
          const data = await releasePackagesGetStatusWorkflow(idOrKey)
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
    /**
     * The lifecycle actions the package's workflow allows from its current status.
     *
     * Tagged as the package's status history, which every status change already invalidates: the
     * answer moves whenever the status does.
     */
    getReleasePackageWorkflowActions: builder.query<
      DeliveryWorkflowActionDto[],
      string
    >({
      queryFn: async (idOrKey) => {
        try {
          const data = await releasePackagesGetWorkflowActions(idOrKey)
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
    assembleReleasePackage: builder.mutation<
      ObjectIdAndKey,
      AssembleReleasePackageRequest
    >({
      queryFn: async (request) => {
        try {
          const data = await releasePackagesAssemble(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.ReleasePackage, id: 'LIST' }],
    }),
    /**
     * Replaces the whole manifest.
     *
     * Components carry no id and cannot be addressed individually, so an edit sends every line the
     * package should end up with — not a delta.
     */
    setReleasePackageManifest: builder.mutation<
      void,
      {
        id: string
        cacheKey: number
        request: SetReleasePackageManifestRequest
      }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await releasePackagesSetManifest(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) =>
        packageTags(arg.id, arg.cacheKey),
    }),
    markReleasePackageReleased: builder.mutation<
      void,
      {
        id: string
        cacheKey: number
        request: MarkReleasePackageReleasedRequest
      }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await releasePackagesMarkReleased(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) =>
        packageTags(arg.id, arg.cacheKey),
    }),
    /**
     * Corrects recorded dates without moving the package's status.
     *
     * Invalidates the package but not its status history, which a correction leaves untouched.
     */
    correctReleasePackageDates: builder.mutation<
      void,
      { id: string; request: CorrectReleasePackageDatesRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await releasePackagesCorrectDates(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.ReleasePackage, id: 'LIST' },
        { type: QueryTags.ReleasePackage, id: arg.id },
        { type: QueryTags.ActivityLog, id: arg.id },
      ],
    }),
    withdrawReleasePackage: builder.mutation<
      void,
      { id: string; cacheKey: number; request: WithdrawReleasePackageRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await releasePackagesWithdraw(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) =>
        packageTags(arg.id, arg.cacheKey),
    }),

    /**
     * Deletes a package and every deployment of it, so the deployment list and the delivery measures
     * are invalidated with it. Only lists: invalidating the deleted package's own tags refetches its
     * page, still mounted until the redirect lands, and every one of those queries 404s.
     */
    deleteReleasePackage: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await releasePackagesDelete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.ReleasePackage, id: 'LIST' },
        { type: QueryTags.Deployment, id: 'LIST' },
        { type: QueryTags.Release, id: 'LIST' },
        { type: QueryTags.DeliveryMetrics, id: 'LIST' },
      ],
    }),

    getReleasePackageActivities: builder.query<
      PagedResponseOfActivityLogDto,
      { idOrKey: string | number; page?: number; pageSize?: number }
    >({
      queryFn: async ({ idOrKey, page, pageSize }) => {
        try {
          const data = await releasePackagesGetActivities(String(idOrKey), {
            page: page,
            pageSize: pageSize,
          })
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
  useGetReleasePackagesQuery,
  useGetReleasePackageQuery,
  useGetReleasePackageStatusHistoryQuery,
  useGetReleasePackageStatusWorkflowQuery,
  useGetReleasePackageWorkflowActionsQuery,
  useAssembleReleasePackageMutation,
  useSetReleasePackageManifestMutation,
  useMarkReleasePackageReleasedMutation,
  useCorrectReleasePackageDatesMutation,
  useWithdrawReleasePackageMutation,
  useDeleteReleasePackageMutation,
  useGetReleasePackageActivitiesQuery,
  useLazyGetReleasePackageActivitiesQuery,
} = releasePackagesApi
