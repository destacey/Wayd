'use client'

import { ReactNode } from 'react'
import RecordHeader, { RecordHeaderProps } from './record-header'
import styles from './record-layout.module.css'

export interface RecordShellProps {
  /** The record this page is about, rendered as the identity bar. */
  record: RecordHeaderProps
  children: ReactNode
}

/**
 * A record page's identity bar and a padded content area — the frame without
 * the furniture.
 *
 * For records whose content is a single interactive surface rather than a set
 * of sections: a story map board, a live poker session. They gain the shared
 * identity bar, and keep a body that would have nowhere to sit inside
 * `RecordLayout`'s rail-and-panel frame.
 *
 * Use `RecordLayout` for anything with sections or record facts. This exists
 * so those pages do not each re-create the full-bleed header wrapper, not as a
 * lighter alternative to it.
 */
const RecordShell = ({ record, children }: RecordShellProps) => (
  <div className={styles.shell}>
    <div className={styles.header}>
      <RecordHeader {...record} />
    </div>
    <div className={styles.content}>{children}</div>
  </div>
)

export default RecordShell
