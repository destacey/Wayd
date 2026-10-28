import { getMyFollowsClient } from '@/src/services/clients'
import {
  FollowCountsDto,
  FollowedRecordDetailDto,
  FollowSort,
  FollowStatusDto,
  FollowView,
  PagedResponseOfFollowedRecordDto,
  SortDirection,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'

/**
 * The kinds of record that can be followed, as the API names them. Each one is
 * registered on the server with the read rule its own pages use.
 */
export const FollowOwnerTypes = {
  Project: 'ppm.project',
  Program: 'ppm.program',
  PlanningIntervalObjective: 'planning.planning-interval-objective',
  IntakeRequest: 'demand.intake-request',
} as const

export type FollowOwnerType =
  (typeof FollowOwnerTypes)[keyof typeof FollowOwnerTypes]

export interface FollowTarget {
  ownerType: FollowOwnerType
  ownerId: string
}

/** What narrows the Following list; paging is handled by the infinite query. */
export interface GetMyFollowsRequest {
  ownerType?: string
  view?: FollowView
  search?: string
  sort: FollowSort
  direction: SortDirection
}

/** How many followed records each page of the list loads. */
export const FollowsPageSize = 50

const statusTag = ({ ownerType, ownerId }: FollowTarget) => ({
  type: QueryTags.FollowStatus,
  id: `${ownerType}:${ownerId}`,
})

/**
 * One followed record's detail. Unfollowing never invalidates it: the pane
 * showing it is on its way out, and a refetch would only earn a 404 for a
 * record the person just stopped following.
 */
const followedRecordTag = ({ ownerType, ownerId }: FollowTarget) => ({
  type: QueryTags.FollowedRecord,
  id: `${ownerType}:${ownerId}`,
})

export const followsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getFollowStatus: builder.query<FollowStatusDto, FollowTarget>({
      queryFn: async ({ ownerType, ownerId }) => {
        try {
          const data = await getMyFollowsClient().getFollowStatus(
            ownerType,
            ownerId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, target) => [statusTag(target)],
    }),

    getMyFollows: builder.infiniteQuery<
      PagedResponseOfFollowedRecordDto,
      GetMyFollowsRequest,
      number
    >({
      infiniteQueryOptions: {
        initialPageParam: 1,
        getNextPageParam: (lastPage) =>
          lastPage.hasNextPage ? lastPage.pageNumber + 1 : undefined,
      },
      queryFn: async ({ queryArg, pageParam }) => {
        try {
          const data = await getMyFollowsClient().getFollows(
            queryArg.ownerType,
            queryArg.view,
            queryArg.search,
            queryArg.sort,
            pageParam,
            FollowsPageSize,
            queryArg.direction,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: [QueryTags.MyFollows],
    }),

    getMyFollowCounts: builder.query<FollowCountsDto, void>({
      queryFn: async () => {
        try {
          const data = await getMyFollowsClient().getFollowCounts()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: [QueryTags.MyFollows],
    }),

    getFollowedRecord: builder.query<FollowedRecordDetailDto, FollowTarget>({
      queryFn: async ({ ownerType, ownerId }) => {
        try {
          const data = await getMyFollowsClient().getFollowedRecord(
            ownerType,
            ownerId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: (result, error, target) => [followedRecordTag(target)],
    }),

    follow: builder.mutation<null, FollowTarget>({
      queryFn: async ({ ownerType, ownerId }) => {
        try {
          await getMyFollowsClient().follow(ownerType, ownerId)
          // The API answers 204 with no body; RTK Query rejects undefined as a result.
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Show the new state as soon as the request starts rather than after the
      // refetch, so a second click acts on what the person sees. Rolled back on
      // failure.
      onQueryStarted: async (target, { dispatch, queryFulfilled }) => {
        const patch = dispatch(
          followsApi.util.updateQueryData(
            'getFollowStatus',
            target,
            (draft) => {
              // followedOn waits for the refetch: a Date in an action fails Redux's
              // serializability check.
              draft.isFollowing = true
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patch.undo()
        }
      },
      invalidatesTags: (result, error, target) => [
        statusTag(target),
        followedRecordTag(target),
        QueryTags.MyFollows,
      ],
    }),

    unfollow: builder.mutation<null, FollowTarget>({
      queryFn: async ({ ownerType, ownerId }) => {
        try {
          await getMyFollowsClient().unfollow(ownerType, ownerId)
          // The API answers 204 with no body; RTK Query rejects undefined as a result.
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      onQueryStarted: async (target, { dispatch, queryFulfilled }) => {
        const patch = dispatch(
          followsApi.util.updateQueryData(
            'getFollowStatus',
            target,
            (draft) => {
              draft.isFollowing = false
              draft.followedOn = undefined
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patch.undo()
        }
      },
      invalidatesTags: (result, error, target) => [
        statusTag(target),
        QueryTags.MyFollows,
      ],
    }),
  }),
})

export const {
  useGetFollowStatusQuery,
  useGetMyFollowsInfiniteQuery,
  useGetMyFollowCountsQuery,
  useGetFollowedRecordQuery,
  useFollowMutation,
  useUnfollowMutation,
} = followsApi
