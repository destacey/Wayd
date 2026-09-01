'use client'

import { StatusNavigationDto } from '@/src/services/wayd-api'
import { Flex, Tooltip } from 'antd'
import { FC } from 'react'
import { statusCategoryDescription } from './status-category'
import WorkflowStatusTag from './workflow-status-tag'

export interface StatusHistoryTagProps {
  status: StatusNavigationDto
  /** Opens the record's status history. Omit to render the tag without interaction. */
  onOpenHistory?: () => void
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
}) => {
  const tag = <WorkflowStatusTag name={status.name} category={status.category} />
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

  if (!title) return tag

  // A span for the same reason the interactive branch has one: the tooltip needs an element it
  // can attach a ref to, and antd's Tag does not forward one.
  if (!onOpenHistory) {
    return (
      <Tooltip title={title}>
        <span>{tag}</span>
      </Tooltip>
    )
  }

  return (
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
    </Tooltip>
  )
}

export default StatusHistoryTag
