'use client'

import { cellMessageId } from './edit-state'
import type { CellMark } from './types'

const STATE_TEXT: Record<CellMark['phase'], string> = {
  unsaved: 'Unsaved.',
  saving: 'Saving.',
  error: 'Not saved.',
}

const VISUALLY_HIDDEN = {
  position: 'absolute',
  width: 1,
  height: 1,
  margin: -1,
  overflow: 'hidden',
  clip: 'rect(0 0 0 0)',
  whiteSpace: 'nowrap',
  border: 0,
} as const

export interface CellEditMessagesProps {
  marksByRow: ReadonlyMap<string, Readonly<Record<string, CellMark>>>
  idPrefix: string
  /** The latest failure, read out as it happens. */
  announcement: string
}

/**
 * What a screen reader hears about edited cells. Each marked cell's state —
 * and its error, when it failed — sits here, outside the table, and the cell
 * points to it with `aria-describedby`; inside the cell it would become part
 * of the cell's name and be read twice. A polite live region announces each
 * failure, wherever focus is.
 */
export function CellEditMessages({
  marksByRow,
  idPrefix,
  announcement,
}: CellEditMessagesProps) {
  const messages = Array.from(marksByRow).flatMap(([rowKey, marks]) =>
    Object.entries(marks).map(([columnId, mark]) => {
      const id = cellMessageId(idPrefix, rowKey, columnId)
      return (
        <span key={id} id={id}>
          {mark.error ?? STATE_TEXT[mark.phase]}
        </span>
      )
    }),
  )
  return (
    <div style={VISUALLY_HIDDEN}>
      {messages}
      <div role="status" aria-live="polite">
        {announcement}
      </div>
    </div>
  )
}
