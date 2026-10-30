'use client'

import { LockOutlined } from '@ant-design/icons'
import { Button, Input, Mentions, Segmented } from 'antd'
import { ReactNode, useState } from 'react'
import PersonAvatar from '@/src/components/common/person-avatar'
import { DiscussionMessageKind } from '@/src/services/wayd-api'
import {
  fromComposerText,
  MentionCandidate,
  mentionTexts,
} from './discussion-mentions'
import styles from './discussion.module.css'

/** What a composer hands its owner to post. */
export interface DiscussionComposerValue {
  kind: DiscussionMessageKind
  subject?: string
  /** The message as stored: markdown with each picked mention written out. */
  body: string
}

export interface DiscussionComposerProps {
  /**
   * Whether the author may write internal notes. When false there is no
   * Reply / Internal note switch at all, rather than a disabled one, so there
   * is nothing to get wrong.
   */
  canPostInternal: boolean
  /** The kind when it is not the author's choice: a reply in a thread, an edit. */
  fixedKind?: DiscussionMessageKind
  /** Whether a subject can be given, which only a top-level message has. */
  showSubject: boolean
  /** Who the composer offers after an @ in a reply. */
  mentionCandidates: MentionCandidate[]
  /** Who it offers in an internal note. Defaults to the reply's list. */
  noteMentionCandidates?: MentionCandidate[]
  /** Who reads a message of each kind, said above the box. */
  audience?: Partial<Record<DiscussionMessageKind, string>>
  /** What an edit starts from. */
  initial?: { subject?: string; text: string; mentioned: MentionCandidate[] }
  submitLabel?: (kind: DiscussionMessageKind) => string
  placeholder?: (kind: DiscussionMessageKind) => string
  /** More controls beside the submit button, shown for a reply only. */
  replyExtra?: ReactNode
  /**
   * Where the @ suggestions open: above the box when it sits at the bottom of
   * the conversation, so the list is not cut off by the page's end.
   */
  suggestionsPlacement?: 'top' | 'bottom'
  /** Posts the value; resolve true to clear the composer. */
  onSubmit: (value: DiscussionComposerValue) => Promise<boolean>
  onCancel?: () => void
}

const defaultSubmitLabel = (kind: DiscussionMessageKind) =>
  kind === DiscussionMessageKind.InternalNote
    ? 'Add internal note'
    : 'Post reply'

const defaultPlaceholder = (kind: DiscussionMessageKind) =>
  kind === DiscussionMessageKind.InternalNote
    ? 'Write an internal note. Mentions here notify only people who can read notes.'
    : 'Write a reply. Type @ to mention someone.'

/**
 * Where a message is written. Switching to an internal note changes how the
 * whole box looks, not only a label, so a note is never sent to a requester by
 * accident.
 */
const DiscussionComposer = ({
  canPostInternal,
  fixedKind,
  showSubject,
  mentionCandidates,
  noteMentionCandidates,
  audience,
  initial,
  submitLabel = defaultSubmitLabel,
  placeholder = defaultPlaceholder,
  replyExtra,
  suggestionsPlacement = 'bottom',
  onSubmit,
  onCancel,
}: DiscussionComposerProps) => {
  const [chosenKind, setChosenKind] = useState(DiscussionMessageKind.Public)
  const [subject, setSubject] = useState(initial?.subject ?? '')
  const [text, setText] = useState(initial?.text ?? '')
  const [mentioned, setMentioned] = useState<MentionCandidate[]>(
    initial?.mentioned ?? [],
  )
  const [isSubmitting, setIsSubmitting] = useState(false)

  const kind = fixedKind ?? chosenKind
  const isNote = kind === DiscussionMessageKind.InternalNote
  const candidates =
    isNote && noteMentionCandidates ? noteMentionCandidates : mentionCandidates
  const hint = audience?.[kind]
  const texts = mentionTexts(candidates)

  const submit = async () => {
    if (!text.trim() || isSubmitting) return
    // Someone picked while writing a reply stays plain text in a note unless the note may name them.
    const mentionable =
      isNote && noteMentionCandidates
        ? mentioned.filter((m) =>
            noteMentionCandidates.some((c) => c.id === m.id),
          )
        : mentioned
    setIsSubmitting(true)
    try {
      const posted = await onSubmit({
        kind,
        subject: showSubject && subject.trim() ? subject.trim() : undefined,
        body: fromComposerText(text, mentionable),
      })
      if (posted && !initial) {
        setSubject('')
        setText('')
        setMentioned([])
      }
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className={styles.composer}>
      {(canPostInternal && !fixedKind) || hint ? (
        <div className={styles.composerHeader}>
          {canPostInternal && !fixedKind && (
            <Segmented
              aria-label="Message type"
              value={chosenKind}
              onChange={(value) =>
                setChosenKind(value as DiscussionMessageKind)
              }
              options={[
                { label: 'Reply', value: DiscussionMessageKind.Public },
                {
                  label: 'Internal note',
                  value: DiscussionMessageKind.InternalNote,
                  icon: <LockOutlined />,
                },
              ]}
            />
          )}
          {hint && (
            <span
              className={`${styles.audience} ${isNote ? styles.audienceNote : ''}`}
            >
              {isNote && <LockOutlined />} {hint}
            </span>
          )}
        </div>
      ) : null}
      <div
        className={`${styles.composerBox} ${isNote ? styles.composerBoxNote : ''}`}
        data-kind={kind}
      >
        {showSubject && (
          <Input
            aria-label="Subject"
            variant="borderless"
            placeholder="Subject (optional)"
            maxLength={200}
            value={subject}
            onChange={(e) => setSubject(e.target.value)}
          />
        )}
        <Mentions
          aria-label={isNote ? 'Internal note' : 'Message'}
          variant="borderless"
          autoSize={{ minRows: 2, maxRows: 12 }}
          placeholder={placeholder(kind)}
          value={text}
          onChange={setText}
          placement={suggestionsPlacement}
          notFoundContent="No one by that name can be mentioned here"
          onSelect={(option) => {
            const person = candidates.find(
              (c) => texts.get(c.id) === option.value,
            )
            if (person && !mentioned.some((m) => m.id === person.id)) {
              setMentioned([
                ...mentioned,
                { id: person.id, name: option.value ?? person.name },
              ])
            }
          }}
          options={candidates.map((c) => ({
            key: c.id,
            value: texts.get(c.id) ?? c.name,
            label: (
              <span className={styles.mentionOption}>
                <PersonAvatar
                  name={c.name}
                  colorKey={c.id}
                  size={20}
                  showTooltip={false}
                />
                {texts.get(c.id) ?? c.name}
              </span>
            ),
          }))}
        />
      </div>
      <div className={styles.composerFooter}>
        {!isNote && replyExtra}
        <span className={styles.toolbarSpacer} />
        <span className={styles.audience}>Markdown supported</span>
        {onCancel && <Button onClick={onCancel}>Cancel</Button>}
        <Button
          type="primary"
          loading={isSubmitting}
          disabled={!text.trim()}
          onClick={submit}
        >
          {submitLabel(kind)}
        </Button>
      </div>
    </div>
  )
}

export default DiscussionComposer
