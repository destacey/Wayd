'use client'

import { Typography } from 'antd'
import styles from '../../wayd-grid/wayd-grid.module.css'
import {
  gridHintsFor,
  type GridHintMode,
  type GridRowActionSupport,
} from './grid-keymap'

const { Text } = Typography

export interface GridKeyHintsProps {
  mode: GridHintMode
  /** The selected cell edits (mode `cell`). */
  cellEditable: boolean
  rows: GridRowActionSupport
}

/**
 * The keys that act on what the user is doing now, in a line below the
 * grid while a cell is selected or edited. Hidden from assistive
 * technology: the help panel and the controls' own labels cover it, and a
 * line that changes with every key would only add noise.
 */
export function GridKeyHints({ mode, cellEditable, rows }: GridKeyHintsProps) {
  const hints = gridHintsFor({ mode, cellEditable, rows })
  return (
    <div className={styles.keyHints} aria-hidden="true" data-grid-key-hints="">
      {hints.map((hint) => (
        <span key={`${hint.keys} ${hint.label}`} className={styles.keyHint}>
          <Text keyboard>{hint.keys}</Text>
          <Text type="secondary">{hint.label}</Text>
        </span>
      ))}
    </div>
  )
}
