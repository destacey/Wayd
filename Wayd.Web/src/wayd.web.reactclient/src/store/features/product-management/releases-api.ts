import { getReleasesClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  CorrectReleaseDatesRequest,
  MarkReleaseReleasedRequest,
  MoveReleaseTargetDateRequest,
  ObjectIdAndKey,
  PlanReleaseRequest,
  ReleaseDto,
  RevertReleaseRequest,
  SetReleaseContentsRequest,
  StatusTransitionDto,
  UpdateReleaseRequest,
  WithdrawReleaseRequest,
} from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'

export interface GetReleasesRequest {
  productId?: string
  statusCategory?: number[]
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
          const data = await getReleasesClient().getReleases(
            request.productId,
            request.statusCategory,
            request.containingVersionId,
          )
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
          const data = await getReleasesClient().getRelease(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [{ type: QueryTags.Release, id: arg }],
    }),
    getReleaseStatusHistory: builder.query<StatusTransitionDto[], string>({
      queryFn: async (idOrKey) => {
        try {
          const data = await getReleasesClient().getStatusHistory(idOrKey)
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
          const data = await getReleasesClient().plan(request)
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
          const data = await getReleasesClient().update(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => releaseTags(arg.id, arg.cacheKey),
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
          const data = await getReleasesClient().setContents(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => releaseTags(arg.id, arg.cacheKey),
    }),
    moveReleaseTargetDate: builder.mutation<
      void,
      { id: string; cacheKey: number; request: MoveReleaseTargetDateRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getReleasesClient().moveTargetDate(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => releaseTags(arg.id, arg.cacheKey),
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
          const data = await getReleasesClient().correctDates(id, request)
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
          const data = await getReleasesClient().markReleased(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => releaseTags(arg.id, arg.cacheKey),
    }),
    withdrawRelease: builder.mutation<
      void,
      { id: string; cacheKey: number; request: WithdrawReleaseRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getReleasesClient().withdraw(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => releaseTags(arg.id, arg.cacheKey),
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
          const data = await getReleasesClient().revert(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => releaseTags(arg.id, arg.cacheKey),
    }),
  }),
})

export const {
  useGetReleasesQuery,
  useGetReleaseQuery,
  useGetReleaseStatusHistoryQuery,
  usePlanReleaseMutation,
  useUpdateReleaseMutation,
  useSetReleaseContentsMutation,
  useMoveReleaseTargetDateMutation,
  useCorrectReleaseDatesMutation,
  useMarkReleaseReleasedMutation,
  useWithdrawReleaseMutation,
  useRevertReleaseMutation,
} = releasesApi
