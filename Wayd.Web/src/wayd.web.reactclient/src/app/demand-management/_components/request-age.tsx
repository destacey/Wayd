'use client'

import { Tag } from 'antd'
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
  Math.max(
    0,
    Math.floor((now.getTime() - new Date(from).getTime()) / DAY_MS),
  )

/** A day count as a reviewer reads it at a glance: "<1d", "12d". */
export const formatDays = (days: number): string =>
  days < 1 ? '<1d' : `${days}d`

/** A day count's colour: none while young, warning once old, error once stale. */
export const ageColor = (days: number): string | undefined =>
  days >= STALE_AFTER_DAYS
    ? 'error'
    : days >= OLD_AFTER_DAYS
      ? 'warning'
      : undefined

export interface AgeTagProps {
  /** The instant the wait started: when the request arrived, or when it entered its status. */
  since: Date | string
  /** What the wait is, for the tooltip: "Submitted", "In this status". */
  label: string
}

/** How long something has waited, coloured once the wait is long. */
export const AgeTag: FC<AgeTagProps> = ({ since, label }) => {
  const days = daysSince(since)
  return (
    <Tag
      color={ageColor(days)}
      title={`${label} ${new Date(since).toLocaleDateString()}`}
      variant="filled"
    >
      {formatDays(days)}
    </Tag>
  )
}
