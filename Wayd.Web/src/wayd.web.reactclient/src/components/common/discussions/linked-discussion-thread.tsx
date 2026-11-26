'use client'

import { Alert, Button, Space, Spin } from 'antd'
import { useEffect, useRef } from 'react'
import { useGetDiscussionMessageQuery } from '@/src/store/features/common/discussions-api'
import { toApiError } from '@/src/utils/problem-details'
import { useDiscussionContext } from './discussion-context'
import DiscussionMessage from './discussion-message'
import styles from './discussion.module.css'
import { scrollIntoViewGently } from './scroll-into-view'

/** The query parameter a link to one message in a record's conversation carries. */
export const LinkedMessageParam = 'message'

export interface LinkedDiscussionThreadProps {
  /** The message the link names: a top-level message or a reply. */
  messageId: string
  onDismiss: () => void
}

/**
 * The thread holding the message a link opened, shown above the conversation
 * and scrolled to, so the reader lands on it wherever it sits in a long
 * conversation instead of paging until it appears. A reply opens its thread
 * with the reply marked, and the reply scrolls itself into view once the
 * thread's replies load.
 */
const LinkedDiscussionThread = ({
  messageId,
  onDismiss,
}: LinkedDiscussionThreadProps) => {
  const { target } = useDiscussionContext()
  const ref = useRef<HTMLElement>(null)
  const linked = useGetDiscussionMessageQuery({ ...target, messageId })
  const parentId = linked.data?.parentMessageId ?? undefined
  const parent = useGetDiscussionMessageQuery(
    { ...target, messageId: parentId ?? '' },
    { skip: !parentId },
  )

  // A reply whose thread cannot be read is still the message the link names, so it shows on its own.
  const replyAlone = !!parentId && !!parent.error
  const thread = parentId ? parent.data : linked.data
  const error = linked.error
  const shownId = replyAlone ? linked.data?.id : thread?.id

  useEffect(() => {
    if (shownId) scrollIntoViewGently(ref.current)
  }, [shownId])

  const dismiss = (
    <Button size="small" onClick={onDismiss}>
      Dismiss
    </Button>
  )

  // Not found is how the server answers a message deleted or one the reader may not see.
  if (error && toApiError(error).status === 404) {
    return (
      <Alert
        type="info"
        showIcon
        title="The linked message is no longer in this conversation, or you can no longer read it."
        action={dismiss}
      />
    )
  }

  if (error) {
    return (
      <Alert
        type="error"
        showIcon
        title="The linked message could not be loaded."
        action={
          <Space>
            <Button size="small" onClick={() => linked.refetch()}>
              Try again
            </Button>
            {dismiss}
          </Space>
        }
      />
    )
  }

  return (
    <section
      ref={ref}
      className={styles.linkedThread}
      aria-label="Linked message"
    >
      <div className={styles.linkedHeader}>
        <span>
          {parentId
            ? 'The reply you were sent to'
            : 'The message you were sent to'}
        </span>
        <span className={styles.toolbarSpacer} />
        <Button type="link" size="small" onClick={onDismiss}>
          Show only the conversation
        </Button>
      </div>
      {replyAlone && linked.data ? (
        <DiscussionMessage
          message={linked.data}
          isReply
          linkedMessageId={messageId}
        />
      ) : thread ? (
        <DiscussionMessage
          message={thread}
          linkedMessageId={messageId}
          threadOpen={!!parentId}
        />
      ) : (
        <Spin />
      )}
    </section>
  )
}

export default LinkedDiscussionThread
