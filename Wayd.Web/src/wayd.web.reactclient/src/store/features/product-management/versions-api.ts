import { getVersionsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  CorrectVersionDatesRequest,
  CutVersionRequest,
  MarkVersionReleasedRequest,
  RevertVersionReleaseRequest,
  MoveVersionTargetDateRequest,
  ObjectIdAndKey,
  PlanVersionRequest,
  VersionDto,
  StatusTransitionDto,
  UpdateVersionRequest,
  WithdrawVersionRequest,
} from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'

export interface GetVersionsRequest {
  productId?: string
  statusCategory?: number[]
}

/**
 * Tags every query and mutation touches for one version.
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
export const versionTags = (id: string, cacheKey: number) => [
  { type: QueryTags.Version, id: 'LIST' },
  { type: QueryTags.Version, id },
  { type: QueryTags.StatusHistory, id },
  { type: QueryTags.StatusHistory, id: String(cacheKey) },
]

export const versionsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    /**
     * Versions, newest first.
     *
     * There is no package filter here. A version carries no foreign key to the package it shipped
     * in — membership is the manifest's to record, so the question is asked from the packages side
     * with `containingVersionId`, which matches on manifest entries. (`containingProductId` is the
     * broader filter: every package carrying *any* version of a product.)
     */
    getVersions: builder.query<VersionDto[], GetVersionsRequest | undefined>({
      queryFn: async (request = {}) => {
        try {
          const data = await getVersionsClient().getVersions(
            request.productId,
            request.statusCategory,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: () => [{ type: QueryTags.Version, id: 'LIST' }],
    }),
    getVersion: builder.query<VersionDto, string>({
      queryFn: async (id) => {
        try {
          const data = await getVersionsClient().getVersion(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [{ type: QueryTags.Version, id: arg }],
    }),
    getVersionStatusHistory: builder.query<StatusTransitionDto[], string>({
      queryFn: async (id) => {
        try {
          const data = await getVersionsClient().getStatusHistory(id)
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
    planVersion: builder.mutation<ObjectIdAndKey, PlanVersionRequest>({
      queryFn: async (request) => {
        try {
          const data = await getVersionsClient().plan(request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [{ type: QueryTags.Version, id: 'LIST' }],
    }),
    updateVersion: builder.mutation<
      void,
      { id: string; cacheKey: number; request: UpdateVersionRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getVersionsClient().update(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => versionTags(arg.id, arg.cacheKey),
    }),
    moveVersionTargetDate: builder.mutation<
      void,
      { id: string; cacheKey: number; request: MoveVersionTargetDateRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getVersionsClient().moveTargetDate(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => versionTags(arg.id, arg.cacheKey),
    }),
    /**
     * Corrects recorded dates without moving the version's status.
     *
     * Invalidates the version but not its status history, which a correction leaves untouched.
     */
    correctVersionDates: builder.mutation<
      void,
      { id: string; request: CorrectVersionDatesRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getVersionsClient().correctDates(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => [
        { type: QueryTags.Version, id: 'LIST' },
        { type: QueryTags.Version, id: arg.id },
      ],
    }),
    cutVersion: builder.mutation<
      void,
      { id: string; cacheKey: number; request: CutVersionRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getVersionsClient().cut(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => versionTags(arg.id, arg.cacheKey),
    }),
    markVersionReleased: builder.mutation<
      void,
      { id: string; cacheKey: number; request: MarkVersionReleasedRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getVersionsClient().markReleased(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => versionTags(arg.id, arg.cacheKey),
    }),
    withdrawVersion: builder.mutation<
      void,
      { id: string; cacheKey: number; request: WithdrawVersionRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getVersionsClient().withdraw(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => versionTags(arg.id, arg.cacheKey),
    }),
    /**
     * Records that a version marked as shipped did not in fact ship.
     *
     * Distinct from withdrawing: that pulls a version which really shipped and is terminal, while this
     * says the record was wrong and moves the version back to a live status.
     */
    revertVersion: builder.mutation<
      void,
      { id: string; cacheKey: number; request: RevertVersionReleaseRequest }
    >({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getVersionsClient().revert(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => versionTags(arg.id, arg.cacheKey),
    }),
  }),
})

export const {
  useGetVersionsQuery,
  useGetVersionQuery,
  useGetVersionStatusHistoryQuery,
  usePlanVersionMutation,
  useUpdateVersionMutation,
  useMoveVersionTargetDateMutation,
  useCorrectVersionDatesMutation,
  useCutVersionMutation,
  useMarkVersionReleasedMutation,
  useWithdrawVersionMutation,
  useRevertVersionMutation,
} = versionsApi
