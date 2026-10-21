import { getVersionsClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  CorrectVersionDatesRequest,
  CutVersionRequest,
  DeliveryWorkflowActionDto,
  MarkVersionReleasedRequest,
  RevertVersionReleaseRequest,
  MoveVersionTargetDateRequest,
  ObjectIdAndKey,
  PlanVersionRequest,
  VersionDto,
  StatusTransitionDto,
  UpdateVersionRequest,
  WithdrawVersionRequest,
  PagedResponseOfActivityLogDto,
  StatusCategory,
} from '@/src/services/wayd-api'
import { QueryTags } from '../query-tags'

export interface GetVersionsRequest {
  productId?: string
  statusCategory?: StatusCategory[]
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

/**
 * Tags a date correction invalidates for one version: the record, and the lifecycle actions its
 * workflow allows — which move when the cut moment is added or cleared, because a revert returns a
 * cut version to Ready and an uncut one to the start of its workflow. Not the status history, which
 * a correction leaves alone. Keyed by id and short key for the reason given on `versionTags`.
 */
export const correctedVersionDatesTags = (id: string, cacheKey: number) => [
  { type: QueryTags.Version, id: 'LIST' },
  { type: QueryTags.Version, id },
  { type: QueryTags.WorkflowActions, id },
  { type: QueryTags.WorkflowActions, id: String(cacheKey) },
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
      providesTags: (result, error, arg) => [
        { type: QueryTags.Version, id: arg },
      ],
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
    /**
     * The lifecycle actions the version's workflow allows from its current status.
     *
     * Tagged as the version's status history, which every status change already invalidates: the
     * answer moves whenever the status does. Its own tag covers the one change that moves the answer
     * without moving the status — a date correction adding or clearing the cut moment.
     */
    getVersionWorkflowActions: builder.query<
      DeliveryWorkflowActionDto[],
      string
    >({
      queryFn: async (idOrKey) => {
        try {
          const data = await getVersionsClient().getWorkflowActions(idOrKey)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, arg) => [
        { type: QueryTags.StatusHistory, id: arg },
        { type: QueryTags.WorkflowActions, id: arg },
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
      invalidatesTags: (result, error, arg) =>
        versionTags(arg.id, arg.cacheKey),
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
      invalidatesTags: (result, error, arg) =>
        versionTags(arg.id, arg.cacheKey),
    }),
    /**
     * Corrects recorded dates without moving the version's status.
     *
     * See `correctedVersionDatesTags` for what it invalidates.
     */
    correctVersionDates: builder.mutation<
      void,
      { id: string; cacheKey: number; request: CorrectVersionDatesRequest }
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
      invalidatesTags: (result, error, arg) =>
        correctedVersionDatesTags(arg.id, arg.cacheKey),
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
      invalidatesTags: (result, error, arg) =>
        versionTags(arg.id, arg.cacheKey),
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
      invalidatesTags: (result, error, arg) =>
        versionTags(arg.id, arg.cacheKey),
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
      invalidatesTags: (result, error, arg) =>
        versionTags(arg.id, arg.cacheKey),
    }),
    /**
     * Deletes a version and every deployment of it, so the deployment list and the delivery measures
     * are invalidated with it. Only lists: invalidating the deleted version's own tags refetches its
     * page, still mounted until the redirect lands, and every one of those queries 404s.
     */
    deleteVersion: builder.mutation<void, string>({
      queryFn: async (id) => {
        try {
          const data = await getVersionsClient().delete(id)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: () => [
        { type: QueryTags.Version, id: 'LIST' },
        { type: QueryTags.Deployment, id: 'LIST' },
        { type: QueryTags.DeliveryMetrics, id: 'LIST' },
      ],
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
      invalidatesTags: (result, error, arg) =>
        versionTags(arg.id, arg.cacheKey),
    }),

    getVersionActivities: builder.query<
      PagedResponseOfActivityLogDto,
      { idOrKey: string | number; page?: number; pageSize?: number }
    >({
      queryFn: async ({ idOrKey, page, pageSize }) => {
        try {
          const data = await getVersionsClient().getActivities(
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
  useGetVersionsQuery,
  useGetVersionQuery,
  useGetVersionStatusHistoryQuery,
  useGetVersionWorkflowActionsQuery,
  usePlanVersionMutation,
  useUpdateVersionMutation,
  useMoveVersionTargetDateMutation,
  useCorrectVersionDatesMutation,
  useCutVersionMutation,
  useMarkVersionReleasedMutation,
  useWithdrawVersionMutation,
  useDeleteVersionMutation,
  useRevertVersionMutation,
  useGetVersionActivitiesQuery,
  useLazyGetVersionActivitiesQuery,
} = versionsApi
