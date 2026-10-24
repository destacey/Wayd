'use client'

import { StatusNavigationDto } from '@/src/services/wayd-api'
import { Flex, Tooltip, Typography } from 'antd'
import { FC, ReactNode, useEffect, useState } from 'react'
import { statusCategoryDescription } from './status-category'
import { formatTimeInStatus, secondsSince } from './time-in-status'
import WorkflowStatusTag from './workflow-status-tag'

export interface StatusHistoryTagProps {
  status: StatusNavigationDto
  /** Opens the record's status history. Omit to render the tag without interaction. */
  onOpenHistory?: () => void
  /**
   * When the record entered this status. Shows how long it has been there beside the tag. A string as
   * well, because generated instants arrive as ISO strings despite their `Date` type.
   */
  enteredOn?: Date | string
}

/**
 * A record's current status in its header, opening the history behind it.
 *
 * The status is the one thing on a record people most often want the story of — what it moved from,
 * when, and who moved it — so the tag is the way in rather than a separate control.
 */
const StatusHistoryTag: FC<StatusHistoryTagProps> = ({
  status,
  onOpenHistory,
  enteredOn,
}) => {
  // The clock is state, refreshed each minute, rather than read during render: reading it in render
  // would make the output impure, and a clock frozen at mount would show a status entered after the
  // page opened as entered "under a minute" ago for as long as the page stays open. A status entered
  // since the last tick reads as no time, which is within a minute of true.
  const [now, setNow] = useState(() => Date.now())
  const showsTimeInStatus = enteredOn !== undefined
  useEffect(() => {
    if (!showsTimeInStatus) return
    const timer = setInterval(() => setNow(Date.now()), 60_000)
    return () => clearInterval(timer)
  }, [showsTimeInStatus])
  const timeInStatus = enteredOn && (
    <Typography.Text type="secondary">
      for {formatTimeInStatus(secondsSince(enteredOn, now))}
    </Typography.Text>
  )

  const tag = (
    <WorkflowStatusTag name={status.name} category={status.category} />
  )
  const description = statusCategoryDescription(status.category)

  // What the status means, then what clicking does — kept apart rather than run together, since
  // they answer different questions.
  const title = onOpenHistory ? (
    <Flex vertical gap={8}>
      {description && <span>{description}</span>}
      <span>Click to view status history.</span>
    </Flex>
  ) : (
    description
  )

  if (!title) return withTimeInStatus(tag, timeInStatus)

  // A span for the same reason the interactive branch has one: the tooltip needs an element it
  // can attach a ref to, and antd's Tag does not forward one.
  if (!onOpenHistory) {
    return withTimeInStatus(
      <Tooltip title={title}>
        <span>{tag}</span>
      </Tooltip>,
      timeInStatus,
    )
  }

  return withTimeInStatus(
    <Tooltip title={title}>
      <span
        role="button"
        tabIndex={0}
        style={{ cursor: 'pointer' }}
        onClick={onOpenHistory}
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault()
            onOpenHistory()
          }
        }}
      >
        {tag}
      </span>
    </Tooltip>,
    timeInStatus,
  )
}

/** The time in status sits outside the tag's tooltip and click target, so it reads as a caption. */
const withTimeInStatus = (tag: ReactNode, timeInStatus: ReactNode) =>
  timeInStatus ? (
    <Flex gap={4} align="center">
      {tag}
      {timeInStatus}
    </Flex>
  ) : (
    tag
  )

export default StatusHistoryTag
