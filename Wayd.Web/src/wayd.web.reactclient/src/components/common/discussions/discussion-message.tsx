'use client'

import {
  ArrowRightOutlined,
  DeleteOutlined,
  LockOutlined,
  MoreOutlined,
} from '@ant-design/icons'
import {
  App,
  Avatar,
  Button,
  Dropdown,
  MenuProps,
  Spin,
  Tag,
  Tooltip,
} from 'antd'
import { useState } from 'react'
import { formatEventTitle } from '@/src/components/common/activities'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import PersonAvatar from '@/src/components/common/person-avatar'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  DiscussionMessageDto,
  DiscussionMessageKind,
} from '@/src/services/wayd-api'
import {
  useDeleteDiscussionMessageMutation,
  useEditDiscussionMessageMutation,
  useGetDiscussionRepliesQuery,
  useReplyToDiscussionMessageMutation,
} from '@/src/store/features/common/discussions-api'
import { formatTimeAgo } from '@/src/utils/time-ago'
import DiscussionComposer from './discussion-composer'
import { canWrite, errorText, useDiscussionContext } from './discussion-context'
import { toComposerText, toDisplayMarkdown } from './discussion-mentions'
import DiscussionRevisionsModal from './discussion-revisions-modal'
import styles from './discussion.module.css'

const When = ({ instant }: { instant: Date }) => (
  <Tooltip title={new Date(instant).toLocaleString()}>
    <span>{formatTimeAgo(instant)}</span>
  </Tooltip>
)

export interface DiscussionMessageProps {
  message: DiscussionMessageDto
  /** Whether this is a reply shown inside its thread. */
  isReply?: boolean
}

/**
 * One message: who wrote it and when, what it says, the change it was posted
 * with, and, for a top-level message, its thread collapsed beneath it.
 */
const DiscussionMessage = ({
  message,
  isReply = false,
}: DiscussionMessageProps) => {
  const { target, access, mentionCandidates, noteMentionCandidates } =
    useDiscussionContext()
  const messageApi = useMessage()
  const [isEditing, setIsEditing] = useState(false)
  const [showRevisions, setShowRevisions] = useState(false)
  const [editMessage] = useEditDiscussionMessageMutation()
  const [deleteMessage, { isLoading: isDeleting }] =
    useDeleteDiscussionMessageMutation()
  const { modal } = App.useApp()

  if (message.isDeleted) {
    return (
      <div className={styles.tombstone}>
        <DeleteOutlined />
        <span>
          Message by {message.author.name} deleted
          {message.deletedBy ? ` by ${message.deletedBy.name}` : ''}
          {message.deletedOn && (
            <>
              {' · '}
              <When instant={message.deletedOn} />
            </>
          )}
        </span>
      </div>
    )
  }

  const isNote = message.kind === DiscussionMessageKind.InternalNote

  const save = async (value: { subject?: string; body: string }) => {
    const response = await editMessage({
      ...target,
      messageId: message.id,
      subject: value.subject,
      body: value.body,
    })
    if ('error' in response) {
      messageApi.error(errorText(response.error, 'Could not save the message.'))
      return false
    }
    setIsEditing(false)
    return true
  }

  const remove = async () => {
    const response = await deleteMessage({ ...target, messageId: message.id })
    if ('error' in response) {
      messageApi.error(
        errorText(response.error, 'Could not delete the message.'),
      )
    }
  }

  // Only what the caller may do; with nothing, there is no menu at all.
  const actions: MenuProps['items'] = [
    ...(message.canEdit && !isEditing
      ? [{ key: 'edit', label: 'Edit', onClick: () => setIsEditing(true) }]
      : []),
    ...(message.canDelete
      ? [
          {
            key: 'delete',
            label: 'Delete',
            danger: true,
            onClick: () =>
              modal.confirm({
                title: 'Delete this message?',
                content:
                  'Its text and earlier versions are removed for everyone. Who deleted it, and when, stays in its place.',
                okText: 'Delete',
                okType: 'danger',
                onOk: remove,
              }),
          },
        ]
      : []),
  ]

  return (
    <article
      className={`${styles.message} ${isNote && !isReply ? styles.note : ''} ${isReply ? styles.reply : ''}`}
      aria-label={`${isNote ? 'Internal note' : 'Message'} by ${message.author.name}`}
    >
      <PersonAvatar
        name={message.author.name}
        colorKey={message.author.id}
        size={isReply ? 24 : 32}
      />
      <div className={styles.messageBody}>
        <div className={styles.messageHeader}>
          <span className={styles.author}>{message.author.name}</span>
          {isNote && !isReply && (
            <Tag icon={<LockOutlined />} color="warning">
              Internal note
            </Tag>
          )}
          <span className={styles.meta}>
            <When instant={message.postedOn} />
          </span>
          {message.editedOn && (
            <Button
              type="link"
              size="small"
              className={styles.meta}
              onClick={() => setShowRevisions(true)}
            >
              edited
            </Button>
          )}
          <span className={styles.toolbarSpacer} />
          {actions.length > 0 && (
            <span className={styles.messageActions}>
              <Dropdown trigger={['click']} menu={{ items: actions }}>
                <Button
                  type="text"
                  size="small"
                  icon={<MoreOutlined />}
                  aria-label="Message actions"
                  loading={isDeleting}
                />
              </Dropdown>
            </span>
          )}
        </div>
        {isEditing ? (
          <DiscussionComposer
            canPostInternal={access.canPostInternal}
            fixedKind={message.kind}
            showSubject={!isReply}
            mentionCandidates={mentionCandidates}
            noteMentionCandidates={noteMentionCandidates}
            initial={{
              subject: message.subject,
              ...toComposerText(message.body ?? '', message.mentions),
            }}
            submitLabel={() => 'Save'}
            onSubmit={save}
            onCancel={() => setIsEditing(false)}
          />
        ) : (
          <>
            {message.subject && (
              <div className={styles.subject}>{message.subject}</div>
            )}
            <MarkdownRenderer
              markdown={toDisplayMarkdown(message.body ?? '', message.mentions)}
            />
          </>
        )}
        {message.recordChange && (
          <span className={styles.recordChange}>
            <ArrowRightOutlined />
            {formatEventTitle(
              message.recordChange.eventType,
              message.recordChange.summary,
            )}
          </span>
        )}
        {!isReply && <DiscussionThread message={message} />}
      </div>
      {showRevisions && (
        <DiscussionRevisionsModal
          message={message}
          onClose={() => setShowRevisions(false)}
        />
      )}
    </article>
  )
}

/**
 * A top-level message's replies: a summary until opened, then the replies in
 * order with a box to add one. A thread is wholly its message's kind, so its
 * reply box writes that kind.
 */
const DiscussionThread = ({ message }: { message: DiscussionMessageDto }) => {
  const { target, access, mentionCandidates, noteMentionCandidates } =
    useDiscussionContext()
  const messageApi = useMessage()
  const [isOpen, setIsOpen] = useState(false)
  const { data: replies, isLoading } = useGetDiscussionRepliesQuery(
    { ...target, messageId: message.id },
    { skip: !isOpen },
  )
  const [reply] = useReplyToDiscussionMessageMutation()
  const mayReply = canWrite(access, message.kind)

  if (!isOpen) {
    if (message.replyCount === 0) {
      return mayReply ? (
        <Button
          type="link"
          size="small"
          className={styles.threadSummary}
          onClick={() => setIsOpen(true)}
        >
          Reply in thread
        </Button>
      ) : null
    }
    return (
      <Button
        type="link"
        size="small"
        className={styles.threadSummary}
        onClick={() => setIsOpen(true)}
        aria-expanded={false}
      >
        <Avatar.Group size="small" max={{ count: 3 }}>
          {message.recentRepliers.map((person) => (
            <PersonAvatar
              key={person.id}
              name={person.name}
              colorKey={person.id}
              size="small"
            />
          ))}
        </Avatar.Group>
        {message.replyCount} {message.replyCount === 1 ? 'reply' : 'replies'}
        {message.lastReplyOn &&
          ` · last reply ${formatTimeAgo(message.lastReplyOn)}`}
      </Button>
    )
  }

  const post = async (value: { body: string }) => {
    const response = await reply({
      ...target,
      parentMessageId: message.id,
      body: value.body,
    })
    if ('error' in response) {
      messageApi.error(errorText(response.error, 'Could not post the reply.'))
      return false
    }
    return true
  }

  // The toggle closes the thread from under its last reply, in the message rather than inside the panel.
  return (
    <>
      <div className={styles.thread}>
        {isLoading && <Spin size="small" />}
        {replies?.map((r) => (
          <DiscussionMessage key={r.id} message={r} isReply />
        ))}
        {mayReply && (
          <DiscussionComposer
            canPostInternal={access.canPostInternal}
            fixedKind={message.kind}
            showSubject={false}
            mentionCandidates={mentionCandidates}
            noteMentionCandidates={noteMentionCandidates}
            compact
            submitLabel={() => 'Reply'}
            placeholder={(kind) =>
              kind === DiscussionMessageKind.InternalNote
                ? 'Reply in thread (internal)…'
                : 'Reply in thread…'
            }
            onSubmit={post}
          />
        )}
      </div>
      <Button
        type="link"
        size="small"
        className={styles.threadSummary}
        onClick={() => setIsOpen(false)}
        aria-expanded
      >
        {message.replyCount > 0 ? 'Hide replies' : 'Close'}
      </Button>
    </>
  )
}

export default DiscussionMessage
