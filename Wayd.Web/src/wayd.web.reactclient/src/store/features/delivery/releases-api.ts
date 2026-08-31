import { getReleasesClient } from '@/src/services/clients'
import { apiSlice } from '../apiSlice'
import {
  CutReleaseRequest,
  MarkReleaseReleasedRequest,
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
 */
const releaseTags = (id: string) => [
  { type: QueryTags.Release, id: 'LIST' },
  { type: QueryTags.Release, id },
  { type: QueryTags.StatusHistory, id },
]

export const releasesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    /**
     * Releases, newest first.
     *
     * No packageId filter: `Release.PackageId` is never written, so the server would answer every
     * such query with nothing. A package's members come from its manifest instead.
     */
    getReleases: builder.query<ReleaseDto[], GetReleasesRequest | undefined>({
      queryFn: async (request = {}) => {
        try {
          const data = await getReleasesClient().getReleases(
            request.productId,
            undefined,
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
      { id: string; request: UpdateReleaseRequest }
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
      invalidatesTags: (result, error, arg) => releaseTags(arg.id),
    }),
    moveReleaseTargetDate: builder.mutation<
      void,
      { id: string; request: MoveReleaseTargetDateRequest }
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
      invalidatesTags: (result, error, arg) => releaseTags(arg.id),
    }),
    cutRelease: builder.mutation<void, { id: string; request: CutReleaseRequest }>({
      queryFn: async ({ id, request }) => {
        try {
          const data = await getReleasesClient().cut(id, request)
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: (result, error, arg) => releaseTags(arg.id),
    }),
    markReleaseReleased: builder.mutation<
      void,
      { id: string; request: MarkReleaseReleasedRequest }
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
      invalidatesTags: (result, error, arg) => releaseTags(arg.id),
    }),
    withdrawRelease: builder.mutation<
      void,
      { id: string; request: WithdrawReleaseRequest }
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
      invalidatesTags: (result, error, arg) => releaseTags(arg.id),
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
  useCutReleaseMutation,
  useMarkReleaseReleasedMutation,
  useWithdrawReleaseMutation,
} = releasesApi
