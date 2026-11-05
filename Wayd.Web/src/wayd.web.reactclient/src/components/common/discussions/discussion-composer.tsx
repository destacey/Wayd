'use client'

import { CloseOutlined, LockOutlined } from '@ant-design/icons'
import { Button, Input, Mentions, Segmented } from 'antd'
import { KeyboardEvent, useState, useSyncExternalStore } from 'react'
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
  /**
   * Where the @ suggestions open: above the box when it sits at the bottom of
   * the conversation, so the list is not cut off by the page's end.
   */
  suggestionsPlacement?: 'top' | 'bottom'
  /** Posts the value; resolve true to clear the composer. */
  onSubmit: (value: DiscussionComposerValue) => Promise<boolean>
  onCancel?: () => void
  /** A reply box inside a thread: one line to start, a small button, and no formatting note. */
  compact?: boolean
}

const noSubscription = () => () => {}

/**
 * The key that posts with Enter: Ctrl, or ⌘ on a Mac. The server renders Ctrl, and the browser's first render
 * agrees with it before switching, so hydration never mismatches.
 */
const usePostKey = () =>
  useSyncExternalStore(
    noSubscription,
    () => (/Mac|iPhone|iPad/.test(navigator.platform) ? '⌘' : 'Ctrl'),
    () => 'Ctrl',
  )

const defaultSubmitLabel = (kind: DiscussionMessageKind) =>
  kind === DiscussionMessageKind.InternalNote
    ? 'Add internal note'
    : 'Post message'

const defaultPlaceholder = (kind: DiscussionMessageKind) =>
  kind === DiscussionMessageKind.InternalNote
    ? 'Write an internal note. Mentions here notify only people who can read notes.'
    : 'Write a message. Type @ to mention someone.'

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
  suggestionsPlacement = 'bottom',
  onSubmit,
  onCancel,
  compact = false,
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

  const postKey = usePostKey()

  /** Ctrl+Enter or ⌘+Enter posts; Enter alone starts a new line, as in a ticket or work item comment. */
  const postOnCtrlEnter = (event: KeyboardEvent) => {
    if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
      event.preventDefault()
      void submit()
    }
  }

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
                { label: 'Message', value: DiscussionMessageKind.Public },
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
        {(subject || text) && !isSubmitting && (
          <Button
            type="text"
            size="small"
            icon={<CloseOutlined />}
            aria-label="Clear"
            title="Clear"
            className={styles.composerClear}
            onClick={() => {
              setSubject('')
              setText('')
              setMentioned([])
            }}
          />
        )}
        {showSubject && (
          <Input
            aria-label="Subject"
            variant="borderless"
            className={styles.composerSubject}
            placeholder="Subject (optional)"
            maxLength={200}
            value={subject}
            onChange={(e) => setSubject(e.target.value)}
            onKeyDown={postOnCtrlEnter}
          />
        )}
        <Mentions
          aria-label={isNote ? 'Internal note' : 'Message'}
          variant="borderless"
          className={showSubject ? undefined : styles.composerFirstField}
          autoSize={{ minRows: compact ? 1 : 2, maxRows: 12 }}
          placeholder={placeholder(kind)}
          value={text}
          onChange={setText}
          onKeyDown={postOnCtrlEnter}
          placement={suggestionsPlacement}
          // Names have spaces, so the search runs on past one while it still starts someone's name, and
          // stops once the text after the @ is plainly the rest of a sentence.
          validateSearch={(search) =>
            !/[\r\n]/.test(search) &&
            (!search.includes(' ') ||
              [...texts.values()].some((t) =>
                t.toLowerCase().startsWith(search.toLowerCase()),
              ))
          }
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
        <span className={styles.toolbarSpacer} />
        {!compact && (
          <span className={styles.audience}>
            Markdown supported · {postKey}+Enter to post
          </span>
        )}
        {onCancel && (
          <Button size={compact ? 'small' : undefined} onClick={onCancel}>
            Cancel
          </Button>
        )}
        <Button
          type="primary"
          size={compact ? 'small' : undefined}
          loading={isSubmitting}
          disabled={!text.trim()}
          title={`${postKey}+Enter`}
          onClick={submit}
        >
          {submitLabel(kind)}
        </Button>
      </div>
    </div>
  )
}

export default DiscussionComposer
