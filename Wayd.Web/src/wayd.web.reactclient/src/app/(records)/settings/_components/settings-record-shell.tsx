'use client'

import { PropsWithChildren } from 'react'
import styles from './settings-record-shell.module.css'

/**
 * Wraps a settings record page so `RecordLayout` runs edge-to-edge.
 *
 * Settings keeps its navigation rail on record pages, so unlike PPM's — which
 * live in `(records)` and get an unpadded frame from the route group — a
 * settings record page renders inside the settings layout's padded content
 * column. `RecordLayout` is built to own its whole area: its identity bar
 * supplies its own 24px and is meant to span the full width, so the padding
 * above it would inset the bar and stop the rail sitting flush.
 *
 * Every settings record page wraps in this rather than repeating the negation,
 * and the settings layout keeps its padding for the list pages, which want it.
 */
const SettingsRecordShell = ({ children }: PropsWithChildren) => (
  <div className={styles.shell}>{children}</div>
)

export default SettingsRecordShell
