'use client'

import { Input, InputRef } from 'antd'
import type { TextAreaRef } from 'antd/es/input/TextArea'
import { FC, KeyboardEvent, Ref, useEffect, useRef, useState } from 'react'
import styles from '@/src/app/planning/story-maps/_components/story-map.module.css'

const { TextArea } = Input

export interface InlineEditTextProps {
  value: string
  onSave: (value: string) => void
  /** Render the static (non-editing) text; lets callers control typography. */
  display: (value: string) => React.ReactNode
  maxLength?: number
  disabled?: boolean
  ariaLabel?: string
  /** Start in edit mode immediately (used right after creating a new item). */
  autoEdit?: boolean
  /**
   * Called when editing ends, whether saved or cancelled. Lets the caller drop the `autoEdit` flag,
   * so remounting the node later — dragging it to another cell, say — does not reopen the editor.
   */
  onEditEnd?: () => void
  className?: string
  /**
   * Edit on a single line instead of the auto-sizing textarea. Use where the value sits in a wide
   * container and never wraps (a swim lane banner); the textarea exists for names in narrow grid
   * columns, where wrapping is what keeps the caret under the click.
   */
  singleLine?: boolean
}

/**
 * Click-to-edit text. Shows the value as plain text; clicking swaps it for an input that saves on
 * Enter/blur and cancels on Escape. Empty input is ignored (keeps the current value). Used for goal
 * names, step names, task titles, and swim lane names on the board.
 */
const InlineEditText: FC<InlineEditTextProps> = ({
  value,
  onSave,
  display,
  maxLength = 128,
  disabled = false,
  ariaLabel,
  autoEdit = false,
  onEditEnd,
  className,
  singleLine = false,
}) => {
  const [isEditing, setIsEditing] = useState(autoEdit)
  const [draft, setDraft] = useState(value)
  const inputRef = useRef<TextAreaRef | InputRef>(null)
  const committedRef = useRef(false)

  useEffect(() => {
    if (isEditing) {
      // Focus and select on entering edit mode.
      const id = setTimeout(() => inputRef.current?.focus({ cursor: 'all' }), 0)
      return () => clearTimeout(id)
    }
  }, [isEditing])

  const startEdit = () => {
    if (disabled) return
    setDraft(value)
    committedRef.current = false
    setIsEditing(true)
  }

  const commit = () => {
    if (committedRef.current) return
    committedRef.current = true
    setIsEditing(false)
    onEditEnd?.()

    const next = draft.trim()
    if (next && next !== value) onSave(next)
  }

  const cancel = () => {
    committedRef.current = true
    setIsEditing(false)
    onEditEnd?.()
  }

  const handleKeyDown = (
    e: KeyboardEvent<HTMLTextAreaElement | HTMLInputElement>,
  ) => {
    // These are single-value names; Enter commits rather than inserting a newline.
    if (e.key === 'Enter') {
      e.preventDefault()
      commit()
    } else if (e.key === 'Escape') {
      e.preventDefault()
      cancel()
    }
  }

  if (isEditing) {
    const editorProps = {
      className: `${styles.inlineEditInput} ${className ?? ''}`,
      maxLength,
      value: draft,
      onChange: (e: { target: { value: string } }) => setDraft(e.target.value),
      onKeyDown: handleKeyDown,
      onBlur: commit,
    }

    // A plain single-line input where the value never wraps (swim lane banners).
    if (singleLine) {
      return <Input ref={inputRef as Ref<InputRef>} {...editorProps} />
    }

    // Otherwise an auto-sizing textarea that matches the display text's box, so the caret lands
    // where the user clicked (including on a wrapped second line) and the layout does not jump.
    return (
      <TextArea ref={inputRef as Ref<TextAreaRef>} autoSize {...editorProps} />
    )
  }

  return (
    <button
      type="button"
      className={`${styles.inlineEditText} ${className ?? ''}`}
      onClick={startEdit}
      disabled={disabled}
      aria-label={ariaLabel}
    >
      {display(value)}
    </button>
  )
}

export default InlineEditText
