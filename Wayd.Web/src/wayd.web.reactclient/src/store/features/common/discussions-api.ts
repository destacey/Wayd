import { getDiscussionsClient } from '@/src/services/clients'
import {
  CursorPageOfDiscussionEntryDto,
  DiscussionDto,
  DiscussionMessageDto,
  DiscussionMessageKind,
  DiscussionMessageRevisionDto,
  DiscussionOrder,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'

/**
 * The kinds of record people can discuss, as the API names them. Each is
 * registered on the server with the rules for who may read and post.
 */
export const DiscussionChannelTypes = {
  IntakeRequest: 'demand.intake-request',
} as const

export type DiscussionChannelType =
  (typeof DiscussionChannelTypes)[keyof typeof DiscussionChannelTypes]

/** One record's conversation. */
export interface DiscussionTarget {
  channelType: DiscussionChannelType
  ownerId: string
}

/** What a page of a conversation shows. Paging is the infinite query's. */
export interface GetDiscussionEntriesRequest extends DiscussionTarget {
  order: DiscussionOrder
  messagesOnly: boolean
}

export interface PostDiscussionMessageArgs extends DiscussionTarget {
  kind: DiscussionMessageKind
  subject?: string
  body: string
}

export interface ReplyToDiscussionMessageArgs extends DiscussionTarget {
  parentMessageId: string
  body: string
}

export interface EditDiscussionMessageArgs extends DiscussionTarget {
  messageId: string
  subject?: string
  body: string
}

export interface DiscussionMessageArgs extends DiscussionTarget {
  messageId: string
}

/** How many top-level messages each page of a conversation loads. */
export const DiscussionPageSize = 25

/**
 * Everything read about one conversation carries one tag, so any change to it
 * refreshes what is on screen: its page of entries, its threads, its access.
 */
export const discussionTag = ({ channelType, ownerId }: DiscussionTarget) => ({
  type: QueryTags.Discussion,
  id: `${channelType}:${ownerId}`,
})

/**
 * What writing a message changes: its conversation, and on a record that gains
 * participants by posting, the record and its history too, so the record's
 * people and Activity show who joined.
 */
const writeTags = (target: DiscussionTarget) => [
  discussionTag(target),
  ...(target.channelType === DiscussionChannelTypes.IntakeRequest
    ? [
        { type: QueryTags.IntakeRequest, id: target.ownerId },
        { type: QueryTags.ActivityLog, id: target.ownerId },
      ]
    : []),
]

const run = async <T>(call: () => Promise<T>) => {
  try {
    return { data: await call() }
  } catch (error) {
    console.error('API Error:', error)
    return { error }
  }
}

/** For an endpoint that answers 204: RTK Query rejects undefined as a result. */
const runVoid = async (call: () => Promise<void>) => {
  try {
    await call()
    return { data: null }
  } catch (error) {
    console.error('API Error:', error)
    return { error }
  }
}

export const discussionsApi = apiSlice.injectEndpoints({
  endpoints: (builder) => ({
    getDiscussion: builder.query<DiscussionDto, DiscussionTarget>({
      queryFn: ({ channelType, ownerId }) =>
        run(() => getDiscussionsClient().getDiscussion(channelType, ownerId)),
      providesTags: (result, error, target) => [discussionTag(target)],
    }),

    getDiscussionEntries: builder.infiniteQuery<
      CursorPageOfDiscussionEntryDto,
      GetDiscussionEntriesRequest,
      string | null
    >({
      infiniteQueryOptions: {
        initialPageParam: null,
        getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
      },
      queryFn: ({ queryArg, pageParam }) =>
        run(() =>
          getDiscussionsClient().getEntries(
            queryArg.channelType,
            queryArg.ownerId,
            pageParam,
            DiscussionPageSize,
            queryArg.order,
            queryArg.messagesOnly,
          ),
        ),
      providesTags: (result, error, request) => [discussionTag(request)],
    }),

    getDiscussionReplies: builder.query<
      DiscussionMessageDto[],
      DiscussionMessageArgs
    >({
      queryFn: ({ channelType, ownerId, messageId }) =>
        run(() =>
          getDiscussionsClient().getReplies(channelType, ownerId, messageId),
        ),
      providesTags: (result, error, args) => [discussionTag(args)],
    }),

    getDiscussionRevisions: builder.query<
      DiscussionMessageRevisionDto[],
      DiscussionMessageArgs
    >({
      queryFn: ({ channelType, ownerId, messageId }) =>
        run(() =>
          getDiscussionsClient().getRevisions(channelType, ownerId, messageId),
        ),
      providesTags: (result, error, args) => [discussionTag(args)],
    }),

    postDiscussionMessage: builder.mutation<string, PostDiscussionMessageArgs>({
      queryFn: ({ channelType, ownerId, kind, subject, body }) =>
        run(() =>
          getDiscussionsClient().postMessage(channelType, ownerId, {
            kind,
            subject,
            body,
          }),
        ),
      invalidatesTags: (result, error, args) => writeTags(args),
    }),

    replyToDiscussionMessage: builder.mutation<
      string,
      ReplyToDiscussionMessageArgs
    >({
      queryFn: ({ channelType, ownerId, parentMessageId, body }) =>
        run(() =>
          getDiscussionsClient().reply(channelType, ownerId, parentMessageId, {
            body,
          }),
        ),
      invalidatesTags: (result, error, args) => writeTags(args),
    }),

    editDiscussionMessage: builder.mutation<null, EditDiscussionMessageArgs>({
      queryFn: ({ channelType, ownerId, messageId, subject, body }) =>
        runVoid(() =>
          getDiscussionsClient().editMessage(channelType, ownerId, messageId, {
            subject,
            body,
          }),
        ),
      invalidatesTags: (result, error, args) => writeTags(args),
    }),

    deleteDiscussionMessage: builder.mutation<null, DiscussionMessageArgs>({
      queryFn: ({ channelType, ownerId, messageId }) =>
        runVoid(() =>
          getDiscussionsClient().deleteMessage(channelType, ownerId, messageId),
        ),
      invalidatesTags: (result, error, args) => [discussionTag(args)],
    }),

    setDiscussionLock: builder.mutation<
      null,
      DiscussionTarget & { locked: boolean }
    >({
      queryFn: ({ channelType, ownerId, locked }) => {
        const client = getDiscussionsClient()
        return runVoid(() =>
          locked
            ? client.lock(channelType, ownerId)
            : client.unlock(channelType, ownerId),
        )
      },
      invalidatesTags: (result, error, args) => [discussionTag(args)],
    }),
  }),
})

export const {
  useGetDiscussionQuery,
  useGetDiscussionEntriesInfiniteQuery,
  useGetDiscussionRepliesQuery,
  useGetDiscussionRevisionsQuery,
  usePostDiscussionMessageMutation,
  useReplyToDiscussionMessageMutation,
  useEditDiscussionMessageMutation,
  useDeleteDiscussionMessageMutation,
  useSetDiscussionLockMutation,
} = discussionsApi
