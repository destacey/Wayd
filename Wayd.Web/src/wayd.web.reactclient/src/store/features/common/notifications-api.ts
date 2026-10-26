import { getMyNotificationsClient } from '@/src/services/clients'
import {
  CursorPageOfNotificationDto,
  UnreadNotificationCountDto,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'

/** A record whose notifications are marked read, as the list names it. */
export interface NotifiedRecordTarget {
  ownerType: string
  ownerId: string
}

/** What narrows the notification list; the infinite query follows each page's cursor. */
export interface GetMyNotificationsRequest {
  unreadOnly?: boolean
  pageSize: number
}

/** How often the unread count is refreshed while the window has focus. */
export const UnreadCountPollingInterval = 60_000

/**
 * Every mark-read changes both the list and the count. Neither is patched in
 * place: the refetch is what drops an entry whose record the reader can no
 * longer see.
 */
const readStateTags = [
  QueryTags.MyNotifications,
  QueryTags.UnreadNotificationCount,
]

export const notificationsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    // Paged by cursor: each page starts after the last entry of the one
    // before, so an entry that moves to the top while the reader loads more is
    // neither repeated nor skipped.
    getMyNotifications: builder.infiniteQuery<
      CursorPageOfNotificationDto,
      GetMyNotificationsRequest,
      string | null
    >({
      infiniteQueryOptions: {
        initialPageParam: null,
        getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
      },
      queryFn: async ({ queryArg, pageParam }) => {
        try {
          const data = await getMyNotificationsClient().getNotifications(
            queryArg.unreadOnly,
            pageParam,
            queryArg.pageSize,
          )
          return { data }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      providesTags: [QueryTags.MyNotifications],
      // Dropped as soon as no list shows it, so reopening never flashes an
      // entry whose record the reader may no longer see.
      keepUnusedDataFor: 0,
    }),

    getUnreadNotificationCount: builder.query<UnreadNotificationCountDto, void>(
      {
        queryFn: async () => {
          try {
            const data = await getMyNotificationsClient().getUnreadCount()
            return { data }
          } catch (error) {
            console.error('API Error:', error)
            return { error }
          }
        },
        providesTags: [QueryTags.UnreadNotificationCount],
      },
    ),

    markRecordNotificationsRead: builder.mutation<null, NotifiedRecordTarget>({
      queryFn: async ({ ownerType, ownerId }) => {
        try {
          await getMyNotificationsClient().markRecordRead(ownerType, ownerId)
          // The API answers 204 with no body; RTK Query rejects undefined as a result.
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: readStateTags,
    }),

    markAllNotificationsRead: builder.mutation<null, void>({
      queryFn: async () => {
        try {
          await getMyNotificationsClient().markAllRead()
          return { data: null }
        } catch (error) {
          console.error('API Error:', error)
          return { error }
        }
      },
      invalidatesTags: readStateTags,
    }),
  }),
})

export const {
  useGetMyNotificationsInfiniteQuery,
  useGetUnreadNotificationCountQuery,
  useMarkRecordNotificationsReadMutation,
  useMarkAllNotificationsReadMutation,
} = notificationsApi
