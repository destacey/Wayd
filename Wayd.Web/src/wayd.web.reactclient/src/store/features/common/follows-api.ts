import { getMyFollowsClient } from '@/src/services/clients'
import { FollowGroupDto, FollowStatusDto } from '@/src/services/wayd-api'
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
} as const

export type FollowOwnerType =
  (typeof FollowOwnerTypes)[keyof typeof FollowOwnerTypes]

export interface FollowTarget {
  ownerType: FollowOwnerType
  ownerId: string
}

export interface GetMyFollowsRequest {
  ownerType?: string
  page?: number
  pageSize?: number
}

const statusTag = ({ ownerType, ownerId }: FollowTarget) => ({
  type: QueryTags.FollowStatus,
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

    getMyFollows: builder.query<FollowGroupDto[], GetMyFollowsRequest>({
      queryFn: async ({ ownerType, page, pageSize }) => {
        try {
          const data = await getMyFollowsClient().getFollows(
            ownerType,
            page,
            pageSize,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: [QueryTags.MyFollows],
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
  useGetMyFollowsQuery,
  useFollowMutation,
  useUnfollowMutation,
} = followsApi
