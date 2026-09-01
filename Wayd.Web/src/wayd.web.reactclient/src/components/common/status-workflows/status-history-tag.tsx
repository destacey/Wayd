'use client'

import { StatusNavigationDto } from '@/src/services/wayd-api'
import { Tooltip } from 'antd'
import { FC } from 'react'
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

  if (!onOpenHistory) return tag

  return (
    <Tooltip title="View status history">
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
