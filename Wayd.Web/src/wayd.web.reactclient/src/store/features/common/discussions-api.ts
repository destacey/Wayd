import {
  getDiscussionSettingsClient,
  getDiscussionsClient,
} from '@/src/services/clients'
import {
  CursorPageOfDiscussionEntryDto,
  DiscussionDto,
  DiscussionMessageDto,
  DiscussionMessageKind,
  DiscussionMessageRevisionDto,
  DiscussionOrder,
  DiscussionSettingsDto,
  SaveDiscussionSettingsRequest,
} from '@/src/services/wayd-api'
import { apiSlice } from '../apiSlice'
import { QueryTags } from '../query-tags'
import {
  DiscussionChannelType,
  DiscussionChannelTypes,
  DiscussionTarget,
  discussionTag,
} from './discussion-tags'

export { DiscussionChannelTypes, discussionTag }
export type { DiscussionChannelType, DiscussionTarget }

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

/**
 * One scope's discussion settings, such as an intake desk's, or the default
 * for a kind of record when `scopeId` is left out.
 */
export interface DiscussionSettingsTarget {
  channelType: DiscussionChannelType
  scopeId?: string
}

export interface SaveDiscussionSettingsArgs
  extends DiscussionSettingsTarget, SaveDiscussionSettingsRequest {}

const settingsTag = ({ channelType, scopeId }: DiscussionSettingsTarget) => ({
  type: QueryTags.DiscussionSettings,
  id: `${channelType}:${scopeId ?? 'default'}`,
})

/** How many top-level messages each page of a conversation loads. */
export const DiscussionPageSize = 25

/**
 * What writing a message changes on the record it is about, by kind of record.
 * Every kind needs an entry, so a new one decides what its posts refresh.
 */
const ownerTags: Record<
  DiscussionChannelType,
  (ownerId: string) => { type: QueryTags; id: string }[]
> = {
  // Posting can make the author or the people mentioned participants, so the
  // request's people and Activity show who joined.
  [DiscussionChannelTypes.IntakeRequest]: (ownerId) => [
    { type: QueryTags.IntakeRequest, id: ownerId },
    { type: QueryTags.ActivityLog, id: ownerId },
  ],
  // Posting changes nothing on a project: access comes from its roles, never
  // from a conversation.
  [DiscussionChannelTypes.Project]: () => [],
}

/** What writing a message changes: its conversation, and its record's caches. */
const writeTags = (target: DiscussionTarget) => [
  discussionTag(target),
  ...ownerTags[target.channelType](target.ownerId),
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

    getDiscussionSettings: builder.query<
      DiscussionSettingsDto,
      DiscussionSettingsTarget
    >({
      queryFn: ({ channelType, scopeId }) =>
        run(() =>
          getDiscussionSettingsClient().getSettings(channelType, scopeId),
        ),
      providesTags: (result, error, args) => [settingsTag(args)],
    }),

    saveDiscussionSettings: builder.mutation<null, SaveDiscussionSettingsArgs>({
      queryFn: ({ channelType, scopeId, ...request }) =>
        runVoid(() =>
          getDiscussionSettingsClient().saveSettings(
            channelType,
            request,
            scopeId,
          ),
        ),
      // A default reaches every scope that follows it, and any setting can
      // change what an open conversation offers, so every one is refreshed.
      invalidatesTags: () => [
        QueryTags.DiscussionSettings,
        QueryTags.Discussion,
      ],
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
  useGetDiscussionSettingsQuery,
  useSaveDiscussionSettingsMutation,
} = discussionsApi
