'use client'

import {
  LockOutlined,
  SortAscendingOutlined,
  SortDescendingOutlined,
  UnlockOutlined,
} from '@ant-design/icons'
import { Alert, Button, Segmented, Spin, Tooltip, Typography } from 'antd'
import { useState } from 'react'
import { formatEventTitle } from '@/src/components/common/activities'
import { useMessage } from '@/src/components/contexts/messaging'
import { useLocalStorageState } from '@/src/hooks'
import {
  ActivityLogDto,
  DiscussionDto,
  DiscussionEntryType,
  DiscussionMessageKind,
  DiscussionOrder,
} from '@/src/services/wayd-api'
import {
  DiscussionTarget,
  useGetDiscussionEntriesInfiniteQuery,
  useGetDiscussionQuery,
  usePostDiscussionMessageMutation,
  useSetDiscussionLockMutation,
} from '@/src/store/features/common/discussions-api'
import { toApiError } from '@/src/utils/problem-details'
import { formatTimeAgo } from '@/src/utils/time-ago'
import DiscussionComposer, {
  DiscussionComposerValue,
} from './discussion-composer'
import { canWrite, DiscussionContext, errorText } from './discussion-context'
import { MentionCandidate } from './discussion-mentions'
import DiscussionMessage from './discussion-message'
import styles from './discussion.module.css'

/** The order a person reads conversations in, kept in their browser. */
export const DiscussionOrderStorageKey = 'wayd.discussion.order'

export interface RecordDiscussionProps extends DiscussionTarget {
  /** Who the composer offers after an @ in a reply. */
  mentionCandidates?: MentionCandidate[]
  /** Who it offers in an internal note. Defaults to the reply's list. */
  noteMentionCandidates?: MentionCandidate[]
  /** Who reads a message of each kind, said above the composer. */
  audience?: Partial<Record<DiscussionMessageKind, string>>
}

const plural = (count: number, one: string, many: string) =>
  `${count} ${count === 1 ? one : many}`

/** How much of the conversation the caller can read, beside its heading. */
const countsText = ({ threadCount, messageCount }: DiscussionDto) =>
  messageCount === 0
    ? 'No messages yet'
    : `${plural(threadCount, 'thread', 'threads')} · ${plural(messageCount, 'message', 'messages')}`

const ActivityEntry = ({ activity }: { activity: ActivityLogDto }) => (
  <li className={styles.activity}>
    <span className={styles.activityDot} aria-hidden="true" />
    <span>
      {activity.employee && (
        <span className={styles.activityActor}>{activity.employee.name} </span>
      )}
      {formatEventTitle(activity.eventType, activity.summary)}
      {' · '}
      {formatTimeAgo(activity.timestamp)}
    </span>
  </li>
)

/**
 * A record's conversation: its messages with their threads, the record's
 * changes between them, and a composer. Read oldest first with the composer at
 * the bottom by default; each person can switch to newest first, which brings
 * the composer to the top.
 *
 * Shows nothing when the caller cannot read the conversation, as the API
 * answers it.
 */
const RecordDiscussion = ({
  channelType,
  ownerId,
  mentionCandidates = [],
  noteMentionCandidates,
  audience,
}: RecordDiscussionProps) => {
  const target: DiscussionTarget = { channelType, ownerId }
  const messageApi = useMessage()
  const [order, setOrder] = useLocalStorageState<DiscussionOrder>(
    DiscussionOrderStorageKey,
    DiscussionOrder.OldestFirst,
    { version: 1 },
  )
  const [messagesOnly, setMessagesOnly] = useState(false)

  const { data: access, error, refetch } = useGetDiscussionQuery(target)
  const {
    data: entries,
    isLoading,
    hasNextPage,
    fetchNextPage,
    isFetchingNextPage,
  } = useGetDiscussionEntriesInfiniteQuery(
    { ...target, order, messagesOnly },
    { skip: !access },
  )
  const [postMessage] = usePostDiscussionMessageMutation()
  const [setLock, { isLoading: isLocking }] = useSetDiscussionLockMutation()

  // Not found is how the server answers a reader who may not see the conversation: show nothing.
  if (error && toApiError(error).status === 404) return null
  if (error) {
    return (
      <Alert
        type="error"
        showIcon
        title="The discussion could not be loaded."
        action={<Button onClick={() => refetch()}>Try again</Button>}
      />
    )
  }
  if (!access) return <Spin />

  const items = entries?.pages.flatMap((page) => page.items) ?? []
  const newestFirst = order === DiscussionOrder.NewestFirst
  const mayPost =
    canWrite(access, DiscussionMessageKind.Public) ||
    canWrite(access, DiscussionMessageKind.InternalNote)

  const post = async (value: DiscussionComposerValue) => {
    const response = await postMessage({ ...target, ...value })
    if ('error' in response) {
      messageApi.error(errorText(response.error, 'Could not post the message.'))
    }
    const posted = !('error' in response)
    // Read oldest first, a new message lands after the pages loaded so far.
    if (posted && !newestFirst && hasNextPage) {
      messageApi.info(
        'Posted. It is at the end of the conversation: choose Show newer to see it.',
      )
    }
    return posted
  }

  const toggleLock = async () => {
    const response = await setLock({ ...target, locked: !access.isLocked })
    if ('error' in response) {
      messageApi.error(
        errorText(response.error, 'Could not change the conversation.'),
      )
    }
  }

  const composer = mayPost ? (
    <DiscussionComposer
      canPostInternal={canWrite(access, DiscussionMessageKind.InternalNote)}
      fixedKind={
        canWrite(access, DiscussionMessageKind.Public)
          ? undefined
          : DiscussionMessageKind.InternalNote
      }
      showSubject
      mentionCandidates={mentionCandidates}
      noteMentionCandidates={noteMentionCandidates}
      audience={audience}
      suggestionsPlacement={newestFirst ? 'bottom' : 'top'}
      onSubmit={post}
    />
  ) : null

  const conversation = (
    <>
      {isLoading ? (
        <Spin />
      ) : (
        <ol className={styles.entries}>
          {items.map((entry) =>
            entry.type === DiscussionEntryType.Message && entry.message ? (
              <li key={entry.message.id}>
                <DiscussionMessage message={entry.message} />
              </li>
            ) : entry.activity ? (
              <ActivityEntry
                key={`activity-${entry.activity.id}`}
                activity={entry.activity}
              />
            ) : null,
          )}
        </ol>
      )}
      {hasNextPage && (
        <Button onClick={() => fetchNextPage()} loading={isFetchingNextPage}>
          {newestFirst ? 'Show older' : 'Show newer'}
        </Button>
      )}
    </>
  )

  return (
    <DiscussionContext.Provider
      value={{ target, access, mentionCandidates, noteMentionCandidates }}
    >
      <section className={styles.discussion} aria-label="Discussion">
        <div className={styles.toolbar}>
          {/* The heading RecordLayout gives every other section, drawn here to sit the counts and filters beside it. */}
          <Typography.Title level={5} style={{ margin: 0 }}>
            Discussion
          </Typography.Title>
          <span className={styles.meta}>{countsText(access)}</span>
          <span className={styles.toolbarSpacer} />
          <Tooltip
            title={newestFirst ? 'Show oldest first' : 'Show newest first'}
          >
            <Button
              type="text"
              size="small"
              icon={
                newestFirst ? (
                  <SortDescendingOutlined />
                ) : (
                  <SortAscendingOutlined />
                )
              }
              onClick={() =>
                setOrder(
                  newestFirst
                    ? DiscussionOrder.OldestFirst
                    : DiscussionOrder.NewestFirst,
                )
              }
            >
              {newestFirst ? 'Newest first' : 'Oldest first'}
            </Button>
          </Tooltip>
          {access.canModerate && (
            <Button
              type="text"
              size="small"
              icon={access.isLocked ? <UnlockOutlined /> : <LockOutlined />}
              loading={isLocking}
              onClick={toggleLock}
            >
              {access.isLocked ? 'Unlock' : 'Lock'}
            </Button>
          )}
          <Segmented
            aria-label="Show"
            value={messagesOnly ? 'messages' : 'all'}
            onChange={(value) => setMessagesOnly(value === 'messages')}
            options={[
              { label: 'Everything', value: 'all' },
              { label: 'Messages only', value: 'messages' },
            ]}
          />
        </div>

        {access.isLocked && (
          <Alert
            type="info"
            showIcon
            icon={<LockOutlined />}
            title="This conversation is locked. Nothing new can be posted, replied or edited."
          />
        )}

        {/* The composer keeps one place in the tree, so switching the order never loses a draft;
            the conversation moves to the other side of it instead. */}
        {!newestFirst && conversation}
        {composer && (
          <div
            className={
              newestFirst ? styles.composerAbove : styles.composerBelow
            }
          >
            {composer}
          </div>
        )}
        {newestFirst && conversation}
      </section>
    </DiscussionContext.Provider>
  )
}

export default RecordDiscussion
