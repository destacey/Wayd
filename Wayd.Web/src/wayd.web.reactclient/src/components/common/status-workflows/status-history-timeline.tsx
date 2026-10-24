'use client'

import { Empty, Flex, Skeleton, Timeline, Typography } from 'antd'
import dayjs from 'dayjs'
import { FC } from 'react'
import { StatusCategory, StatusTransitionDto } from '@/src/services/wayd-api'
import { formatTimeInStatus } from './time-in-status'
import WorkflowStatusTag from './workflow-status-tag'

const { Text } = Typography

export interface StatusHistoryTimelineProps {
  transitions: StatusTransitionDto[] | undefined
  isLoading: boolean
  /** What to say when the record has no recorded changes. Names the record type. */
  emptyDescription?: string
}

type TimelineColor = 'blue' | 'red' | 'green' | 'gray'

const timelineColor = (category: StatusCategory): TimelineColor => {
  switch (category) {
    case StatusCategory.Active:
      return 'blue'
    case StatusCategory.Done:
      return 'green'
    case StatusCategory.Removed:
      return 'red'
    default:
      return 'gray'
  }
}

/**
 * Who made a change, preferring the person over the account.
 *
 * The two can differ: an import records the employee a row is about while carrying the account that
 * ran it. Only the recorded account can say the platform acted — an account since deleted, or one
 * never linked to an employee, also leaves both names empty and must not read as the system.
 */
const changedByLabel = (entry: StatusTransitionDto): string => {
  if (entry.changedBy) return entry.changedBy.name
  if (entry.changedByUser)
    return entry.changedByUser.name ?? entry.changedByUser.userName
  return entry.changedBySystem ? 'System' : 'Unknown'
}

/**
 * How long the record stayed in the status this entry moved it into. The current status has no
 * `leftOn`, and its figure was measured when the history was read.
 */
const timeInStatusLabel = (entry: StatusTransitionDto): string => {
  const duration = formatTimeInStatus(entry.durationSeconds)
  const label = entry.leftOn
    ? `${duration} in ${entry.toStatus.name}`
    : `In ${entry.toStatus.name} for ${duration}`
  // A record that came back to a status shows the total beside each stay, so the time it has spent
  // there overall does not have to be added up by hand.
  return entry.statusVisitCount > 1
    ? `${label} (${formatTimeInStatus(entry.statusTotalSeconds)} across ${entry.statusVisitCount} stays)`
    : label
}

const StatusHistoryTimeline: FC<StatusHistoryTimelineProps> = ({
  transitions,
  isLoading,
  emptyDescription = 'No status changes have been recorded for this record.',
}) => {
  if (isLoading) {
    return <Skeleton active paragraph={{ rows: 4 }} />
  }

  if (!transitions || transitions.length === 0) {
    return <Empty description={emptyDescription} />
  }

  const items = transitions.map((entry) => ({
    // Keyed on the transition, not the index: the list is newest-first and a later change prepends,
    // which would otherwise re-key every row.
    key: entry.id,
    color: timelineColor(entry.toStatus.category),
    content: (
      <Flex vertical gap={2}>
        <Flex gap="small" align="center" wrap>
          {entry.fromStatus && (
            <>
              <WorkflowStatusTag
                name={entry.fromStatus.name}
                category={entry.fromStatus.category}
              />
              <Text type="secondary">→</Text>
            </>
          )}
          <WorkflowStatusTag
            name={entry.toStatus.name}
            category={entry.toStatus.category}
          />
        </Flex>
        <Text type="secondary">
          {dayjs(entry.changedOn).format('MMM D, YYYY h:mm A')} by{' '}
          {changedByLabel(entry)}
        </Text>
        <Text type="secondary">{timeInStatusLabel(entry)}</Text>
        {entry.reason && <Text>{entry.reason}</Text>}
      </Flex>
    ),
  }))

  return <Timeline items={items} />
}

export default StatusHistoryTimeline
