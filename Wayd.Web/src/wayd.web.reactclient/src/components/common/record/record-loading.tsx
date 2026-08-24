'use client'

import { Skeleton } from 'antd'
import RecordHeader from './record-header'
import styles from './record-layout.module.css'

export interface RecordLoadingProps {
  /** Shown in the identity bar while the record loads. */
  title: string
}

/**
 * Route-level loading state for a record page.
 *
 * Mirrors `RecordLayout`'s own frame — full-bleed identity bar, padded content
 * below — because it renders *instead of* the layout, not inside it, so it
 * would otherwise sit flush against the viewport with no padding of its own.
 * Matching the frame also stops the header jumping when the record arrives.
 */
const RecordLoading = ({ title }: RecordLoadingProps) => (
  <div className={styles.shell}>
    <div className={styles.header}>
      <RecordHeader name={title} />
    </div>
    <div className={styles.content}>
      <Skeleton active />
    </div>
  </div>
)

export default RecordLoading
