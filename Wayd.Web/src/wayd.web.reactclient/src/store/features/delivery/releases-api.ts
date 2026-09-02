import { getReleasesClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  CorrectReleaseDatesRequest,
  CutReleaseRequest,
  MarkReleaseReleasedRequest,
  RevertReleaseRequest,
  MoveReleaseTargetDateRequest,
  ObjectIdAndKey,
  PlanReleaseRequest,
  ReleaseDto,
  StatusTransitionDto,
  UpdateReleaseRequest,
  WithdrawReleaseRequest,
} from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'

export interface GetReleasesRequest {
  productId?: string
  statusCategory?: number[]
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
     * Releases, newest first.
     *
     * There is no package filter here. A release carries no foreign key to the package it shipped
     * in — membership is the manifest's to record, so the question is asked from the packages side
     * with `containingReleaseId`, which matches on manifest entries. (`containingProductId` is the
     * broader filter: every package carrying *any* release of a product.)
     */
    getReleases: builder.query<ReleaseDto[], GetReleasesRequest | undefined>({
      queryFn: async (request = {}) => {
        try {
          const data = await getReleasesClient().getReleases(
            request.productId,
            request.statusCategory,
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
      queryFn: async (id) => {
        try {
          const data = await getReleasesClient().getRelease(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [{ type: QueryTags.Release, id: arg }],
    }),
    getReleaseStatusHistory: builder.query<StatusTransitionDto[], string>({
      queryFn: async (id) => {
        try {
          const data = await getReleasesClient().getStatusHistory(id)
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
    cutRelease: builder.mutation<
      void,
      { id: string; cacheKey: number; request: CutReleaseRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getReleasesClient().cut(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => releaseTags(arg.id, arg.cacheKey),
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
     * Records that a release marked as shipped did not in fact ship.
     *
     * Distinct from withdrawing: that pulls a release which really shipped and is terminal, while this
     * says the record was wrong and moves the release back to a live status.
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
  useMoveReleaseTargetDateMutation,
  useCorrectReleaseDatesMutation,
  useCutReleaseMutation,
  useMarkReleaseReleasedMutation,
  useWithdrawReleaseMutation,
  useRevertReleaseMutation,
} = releasesApi
