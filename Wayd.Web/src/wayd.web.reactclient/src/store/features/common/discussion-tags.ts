import { QueryTags } from '../query-tags'

/**
 * The kinds of record people can discuss, as the API names them. Each is
 * registered on the server with the rules for who may read and post.
 */
export const DiscussionChannelTypes = {
  IntakeRequest: 'demand.intake-request',
  Project: 'ppm.project',
} as const

export type DiscussionChannelType =
  (typeof DiscussionChannelTypes)[keyof typeof DiscussionChannelTypes]

/** One record's conversation. */
export interface DiscussionTarget {
  channelType: DiscussionChannelType
  ownerId: string
}

/**
 * Everything read about one conversation carries one tag, so any change to it
 * refreshes what is on screen: its page of entries, its threads, its access.
 *
 * Kept apart from the discussions api so a record's own api can refresh its
 * conversation without importing that module, which registers its endpoints
 * as a side effect.
 */
export const discussionTag = ({ channelType, ownerId }: DiscussionTarget) => ({
  type: QueryTags.Discussion,
  id: `${channelType}:${ownerId}`,
})
