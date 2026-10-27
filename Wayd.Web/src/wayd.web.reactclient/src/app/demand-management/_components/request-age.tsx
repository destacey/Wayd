'use client'

import styles from './request-age.module.css'
import { FC } from 'react'

const DAY_MS = 24 * 60 * 60 * 1000

/**
 * How long a request has waited before it reads as old, and as stale. Reviewers scan for the long
 * waits, so only those are coloured; anything younger is plain.
 */
export const OLD_AFTER_DAYS = 7
export const STALE_AFTER_DAYS = 21

/**
 * Whole days from the instant to now, never negative.
 *
 * Takes a string too: the generated client types an Instant as `Date`, but the response is parsed as plain
 * JSON, so at runtime it is the ISO string.
 */
export const daysSince = (
  from: Date | string,
  now: Date = new Date(),
): number =>
  Math.max(0, Math.floor((now.getTime() - new Date(from).getTime()) / DAY_MS))

/** A day count as a reviewer reads it at a glance: "<1d", "12d". */
export const formatDays = (days: number): string =>
  days < 1 ? '<1d' : `${days}d`

/** How a day count is coloured: plain while young, warning once old, danger once stale. */
export const ageTone = (days: number): 'warning' | 'danger' | undefined =>
  days >= STALE_AFTER_DAYS
    ? 'danger'
    : days >= OLD_AFTER_DAYS
      ? 'warning'
      : undefined

export interface WaitTimeProps {
  /** The instant the wait started: when the request arrived, or when it entered its status. */
  since: Date | string
  /** What the wait is, for the tooltip: "Submitted", "In this status". */
  label: string
}

/**
 * How long something has waited, as plain text coloured only once the wait is long, so a column of them
 * stays quiet and the long waits stand out. A bare span rather than Typography.Text, which sets its own
 * font size and would outsize the grid's cells.
 */
export const WaitTime: FC<WaitTimeProps> = ({ since, label }) => {
  const days = daysSince(since)
  const tone = ageTone(days)
  return (
    <span
      className={tone ? styles[tone] : undefined}
      title={`${label} ${new Date(since).toLocaleDateString()}`}
    >
      {formatDays(days)}
    </span>
  )
}
