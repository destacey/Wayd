'use client'

import { createContext, useContext } from 'react'
import { DiscussionDto, DiscussionMessageKind } from '@/src/services/wayd-api'
import { DiscussionTarget } from '@/src/store/features/common/discussions-api'
import { toApiError } from '@/src/utils/problem-details'
import { MentionCandidate } from './discussion-mentions'

/** What every part of one conversation needs: which it is and what the caller may do in it. */
export interface DiscussionContextValue {
  target: DiscussionTarget
  access: DiscussionDto
  mentionCandidates: MentionCandidate[]
  noteMentionCandidates?: MentionCandidate[]
}

export const DiscussionContext = createContext<DiscussionContextValue | null>(
  null,
)

export const useDiscussionContext = () => {
  const context = useContext(DiscussionContext)
  if (!context) {
    throw new Error('Discussion parts must be inside a RecordDiscussion.')
  }
  return context
}

/**
 * Whether the caller may write a message of the kind: post one, reply in its
 * thread or edit their own. Nobody writes in a locked conversation.
 */
export const canWrite = (access: DiscussionDto, kind: DiscussionMessageKind) =>
  !access.isLocked &&
  (kind === DiscussionMessageKind.InternalNote
    ? access.canPostInternal
    : access.canPost)

/** What went wrong, in the API's words when it gave some. */
export const errorText = (error: unknown, fallback: string) => {
  const apiError = toApiError(error)
  return (
    apiError.detail ?? Object.values(apiError.errors ?? {})[0]?.[0] ?? fallback
  )
}
