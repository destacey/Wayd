import {
  releasesCorrectDates,
  releasesDelete,
  releasesGetActivities,
  releasesGetRelease,
  releasesGetReleases,
  releasesGetStatusHistory,
  releasesGetStatusWorkflow,
  releasesGetWorkflowActions,
  releasesMarkReleased,
  releasesMoveTargetDate,
  releasesPlan,
  releasesRevert,
  releasesSetContents,
  releasesUpdate,
  releasesWithdraw,
} from '@/src/services/api/releases'
import { apiSlice } from '../apiSlice'
import {
  CorrectReleaseDatesRequest,
  DeliveryWorkflowActionDto,
  MarkReleaseReleasedRequest,
  MoveReleaseTargetDateRequest,
  ObjectIdAndKey,
  PlanReleaseRequest,
  ReleaseDto,
  RevertReleaseRequest,
  SetReleaseContentsRequest,
  StatusTransitionDto,
  StatusWorkflowDetailsDto,
  UpdateReleaseRequest,
  WithdrawReleaseRequest,
  PagedResponseOfActivityLogDto,
  StatusCategory,
} from '@/src/services/api/wayd.schemas'
import { QueryTags } from '../query-tags'

export interface GetReleasesRequest {
  productId?: string
  statusCategory?: StatusCategory[]
  containingVersionId?: string
}

/**
 * Tags every query and mutation touches for one release.
 *
 * A status change moves the record, its place in the list, and its history all at once, so they
 * invalidate together rather than each mutation remembering the full set.
 *
 * `cacheKey` is the record's short key, following the convention the app's other slices use. It is
 * required rather than optional because the two sides of the cache address a record differently: a
 * detail page queries its history by the key in its URL, while a mutation holds only the id.
 * Invalidating one alone leaves the other's entry untouched and the history silently stale — and an
 * optional parameter would let a new call site reintroduce exactly that.
 */
export const releaseTags = (id: string, cacheKey: number) => [
  { type: QueryTags.Release, id: 'LIST' },
  { type: QueryTags.Release, id },
  { type: QueryTags.StatusHistory, id },
  { type: QueryTags.StatusHistory, id: String(cacheKey) },
]

export const releasesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    /**
     * Releases, unannounced first and then newest announced.
     *
     * `productId` deliberately excludes releases that name no product: one spanning product lines
     * belongs to no single product, so listing it under one would misstate what that product
     * announced.
     *
     * `containingVersionId` matches a version reached by either route — carried directly, or shipped
     * inside one of the release's packages — which is what makes "where was this announced?"
     * answerable from a version.
     */
    getReleases: builder.query<ReleaseDto[], GetReleasesRequest | undefined>({
      queryFn: async (request = {}) => {
        try {
          const data = await releasesGetReleases({
            productId: request.productId,
            statusCategory: request.statusCategory,
            containingVersionId: request.containingVersionId,
          })
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.Release, id: 'LIST' }],
    }),
    getRelease: builder.query<ReleaseDto, string>({
      queryFn: async (idOrKey) => {
        try {
          const data = await releasesGetRelease(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.Release, id: arg },
      ],
    }),
    getReleaseStatusHistory: builder.query<StatusTransitionDto[], string>({
      queryFn: async (idOrKey) => {
        try {
          const data = await releasesGetStatusHistory(idOrKey)
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
     * The workflow the release moves through, to draw its history on. Tagged with the history too: a
     * reassignment records a transition, which is what moves the release onto another workflow.
     */
    getReleaseStatusWorkflow: builder.query<StatusWorkflowDetailsDto, string>({
      queryFn: async (idOrKey) => {
        try {
          const data = await releasesGetStatusWorkflow(idOrKey)
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
     * The lifecycle actions the release's workflow allows from its current status.
     *
     * Tagged as the release's status history, which every status change already invalidates: the
     * answer moves whenever the status does.
     */
    getReleaseWorkflowActions: builder.query<
      DeliveryWorkflowActionDto[],
      string
    >({
      queryFn: async (idOrKey) => {
        try {
          const data = await releasesGetWorkflowActions(idOrKey)
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
    planRelease: builder.mutation<ObjectIdAndKey, PlanReleaseRequest>({
      queryFn: async (request) => {
        try {
          const data = await releasesPlan(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.Release, id: 'LIST' }],
    }),
    updateRelease: builder.mutation<
      void,
      { id: string; cacheKey: number; request: UpdateReleaseRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await releasesUpdate(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) =>
        releaseTags(arg.id, arg.cacheKey),
    }),
    /**
     * Replaces everything the release announces, both routes at once.
     *
     * One mutation rather than two because the rule that a version is announced once spans the two
     * routes: sent separately, each half would be judged against a release only half-changed, and
     * moving a version into the package that carries it would depend on which half went first.
     */
    setReleaseContents: builder.mutation<
      void,
      { id: string; cacheKey: number; request: SetReleaseContentsRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await releasesSetContents(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) =>
        releaseTags(arg.id, arg.cacheKey),
    }),
    moveReleaseTargetDate: builder.mutation<
      void,
      { id: string; cacheKey: number; request: MoveReleaseTargetDateRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await releasesMoveTargetDate(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) =>
        releaseTags(arg.id, arg.cacheKey),
    }),
    /**
     * Corrects recorded dates without moving the release's status.
     *
     * Invalidates the release but not its status history, which a correction leaves untouched.
     */
    correctReleaseDates: builder.mutation<
      void,
      { id: string; request: CorrectReleaseDatesRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await releasesCorrectDates(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.Release, id: 'LIST' },
        { type: QueryTags.Release, id: arg.id },
      ],
    }),
    markReleaseReleased: builder.mutation<
      void,
      { id: string; cacheKey: number; request: MarkReleaseReleasedRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await releasesMarkReleased(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) =>
        releaseTags(arg.id, arg.cacheKey),
    }),
    withdrawRelease: builder.mutation<
      void,
      { id: string; cacheKey: number; request: WithdrawReleaseRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await releasesWithdraw(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) =>
        releaseTags(arg.id, arg.cacheKey),
    }),
    /**
     * Deletes a release. The packages it listed become deletable once no release lists them, so their
     * cache is invalidated with it. Only lists: invalidating the deleted release's own tags refetches
     * its page, still mounted until the redirect lands, and every one of those queries 404s.
     */
    deleteRelease: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await releasesDelete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.Release, id: 'LIST' },
        { type: QueryTags.ReleasePackage, id: 'LIST' },
      ],
    }),
    /**
     * Records that a release marked as announced was not in fact announced.
     *
     * Distinct from withdrawing: that retracts an announcement which really happened and is terminal,
     * while this says the record was wrong and moves the release back to a live status.
     */
    revertRelease: builder.mutation<
      void,
      { id: string; cacheKey: number; request: RevertReleaseRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await releasesRevert(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) =>
        releaseTags(arg.id, arg.cacheKey),
    }),

    getReleaseActivities: builder.query<
      PagedResponseOfActivityLogDto,
      { idOrKey: string | number; page?: number; pageSize?: number }
    >({
      queryFn: async ({ idOrKey, page, pageSize }) => {
        try {
          const data = await releasesGetActivities(String(idOrKey), {
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
  useGetReleasesQuery,
  useGetReleaseQuery,
  useGetReleaseStatusHistoryQuery,
  useGetReleaseStatusWorkflowQuery,
  useGetReleaseWorkflowActionsQuery,
  usePlanReleaseMutation,
  useUpdateReleaseMutation,
  useSetReleaseContentsMutation,
  useMoveReleaseTargetDateMutation,
  useCorrectReleaseDatesMutation,
  useMarkReleaseReleasedMutation,
  useWithdrawReleaseMutation,
  useDeleteReleaseMutation,
  useRevertReleaseMutation,
  useGetReleaseActivitiesQuery,
  useLazyGetReleaseActivitiesQuery,
} = releasesApi
