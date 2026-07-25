'use client'

import { Input } from 'antd'
import type { TextAreaRef } from 'antd/es/input/TextArea'
import { FC, KeyboardEvent, useEffect, useRef, useState } from 'react'
import styles from '../../_components/story-map.module.css'

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
  className?: string
}

/**
 * Click-to-edit text. Shows the value as plain text; clicking swaps it for an input that saves on
 * Enter/blur and cancels on Escape. Empty input is ignored (keeps the current value). Used for goal
 * names, step names, and task titles on the board.
 */
const InlineEditText: FC<InlineEditTextProps> = ({
  value,
  onSave,
  display,
  maxLength = 128,
  disabled = false,
  ariaLabel,
  autoEdit = false,
  className,
}) => {
  const [isEditing, setIsEditing] = useState(autoEdit)
  const [draft, setDraft] = useState(value)
  const inputRef = useRef<TextAreaRef>(null)
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

    const next = draft.trim()
    if (next && next !== value) onSave(next)
  }

  const cancel = () => {
    committedRef.current = true
    setIsEditing(false)
  }

  const handleKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
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
    // An auto-sizing textarea that matches the display text's box, so the caret lands where the user
    // clicked (including on a wrapped second line) and the layout does not jump on edit.
    return (
      <TextArea
        ref={inputRef}
        className={`${styles.inlineEditInput} ${className ?? ''}`}
        autoSize
        maxLength={maxLength}
        value={draft}
        onChange={(e) => setDraft(e.target.value)}
        onKeyDown={handleKeyDown}
        onBlur={commit}
      />
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
