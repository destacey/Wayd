import { getMyNotificationMutesClient } from '@/src/services/clients'
import {
  MuteRecordResponse,
  NotificationReason,
  PagedResponseOfMutedRecordDto,
  ReasonMuteDto,
  RecordMuteStatusDto,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'

/** A record muted or unmuted, as the API names it. */
export interface MuteTarget {
  ownerType: string
  ownerId: string
}

/** How many muted records each page of the list loads. */
export const MutedRecordsPageSize = 25

const statusTag = ({ ownerType, ownerId }: MuteTarget) => ({
  type: QueryTags.RecordMuteStatus,
  id: `${ownerType}:${ownerId}`,
})

export const notificationMutesApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getRecordMuteStatus: builder.query<RecordMuteStatusDto, MuteTarget>({
      queryFn: async ({ ownerType, ownerId }) => {
        try {
          const data = await getMyNotificationMutesClient().getRecordMuteStatus(
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

    getMutedRecords: builder.infiniteQuery<
      PagedResponseOfMutedRecordDto,
      void,
      number
    >({
      infiniteQueryOptions: {
        initialPageParam: 1,
        getNextPageParam: (lastPage) =>
          lastPage.hasNextPage ? lastPage.pageNumber + 1 : undefined,
      },
      queryFn: async ({ pageParam }) => {
        try {
          const data = await getMyNotificationMutesClient().getMutedRecords(
            pageParam,
            MutedRecordsPageSize,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: [QueryTags.MyMutes],
    }),

    getReasonMutes: builder.query<ReasonMuteDto[], void>({
      queryFn: async () => {
        try {
          const data = await getMyNotificationMutesClient().getReasonMutes()
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: [QueryTags.MyMutes],
    }),

    muteRecord: builder.mutation<MuteRecordResponse, MuteTarget>({
      queryFn: async ({ ownerType, ownerId }) => {
        try {
          const data = await getMyNotificationMutesClient().muteRecord(
            ownerType,
            ownerId,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Show the new state as soon as the request starts, so a second click
      // acts on what the person sees. Rolled back on failure.
      onQueryStarted: async (target, { dispatch, queryFulfilled }) => {
        const patch = dispatch(
          notificationMutesApi.util.updateQueryData(
            'getRecordMuteStatus',
            target,
            (draft) => {
              draft.isMuted = true
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patch.undo()
        }
      },
      // Muting also marks the record's notifications read, so the list and the
      // count change with it.
      invalidatesTags: (result, error, target) => [
        statusTag(target),
        QueryTags.MyMutes,
        QueryTags.MyFollows,
        QueryTags.FollowedRecord,
        QueryTags.MyNotifications,
        QueryTags.UnreadNotificationCount,
      ],
    }),

    unmuteRecord: builder.mutation<null, MuteTarget>({
      queryFn: async ({ ownerType, ownerId }) => {
        try {
          await getMyNotificationMutesClient().unmuteRecord(ownerType, ownerId)
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      onQueryStarted: async (target, { dispatch, queryFulfilled }) => {
        const patch = dispatch(
          notificationMutesApi.util.updateQueryData(
            'getRecordMuteStatus',
            target,
            (draft) => {
              draft.isMuted = false
              draft.mutedOn = undefined
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
        QueryTags.MyMutes,
        QueryTags.MyFollows,
        QueryTags.FollowedRecord,
      ],
    }),

    muteReason: builder.mutation<null, NotificationReason>({
      queryFn: async (reason) => {
        try {
          await getMyNotificationMutesClient().muteReason(reason)
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      // Flips the reason's switch as soon as the request starts, so it does not
      // snap back while the list refetches. Rolled back on failure.
      onQueryStarted: async (reason, { dispatch, queryFulfilled }) => {
        const patch = dispatch(
          notificationMutesApi.util.updateQueryData(
            'getReasonMutes',
            undefined,
            (draft) => {
              const mute = draft.find((m) => m.reason === reason)
              if (mute) mute.isMuted = true
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patch.undo()
        }
      },
      invalidatesTags: [QueryTags.MyMutes],
    }),

    unmuteReason: builder.mutation<null, NotificationReason>({
      queryFn: async (reason) => {
        try {
          await getMyNotificationMutesClient().unmuteReason(reason)
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      onQueryStarted: async (reason, { dispatch, queryFulfilled }) => {
        const patch = dispatch(
          notificationMutesApi.util.updateQueryData(
            'getReasonMutes',
            undefined,
            (draft) => {
              const mute = draft.find((m) => m.reason === reason)
              if (mute) {
                mute.isMuted = false
                mute.mutedOn = undefined
              }
            },
          ),
        )
        try {
          await queryFulfilled
        } catch {
          patch.undo()
        }
      },
      invalidatesTags: [QueryTags.MyMutes],
    }),
  }),
})

export const {
  useGetRecordMuteStatusQuery,
  useGetMutedRecordsInfiniteQuery,
  useGetReasonMutesQuery,
  useMuteRecordMutation,
  useUnmuteRecordMutation,
  useMuteReasonMutation,
  useUnmuteReasonMutation,
} = notificationMutesApi
