'use client'

import { WorkItemDetailsDto } from '@/src/services/wayd-api'
import { Card, Timeline, TimelineItemProps } from 'antd'
import dayjs from 'dayjs'

export interface WorkItemTimelineProps {
  workItem: WorkItemDetailsDto
}

const WorkItemTimeline = ({ workItem }: WorkItemTimelineProps) => {
  if (!workItem) return null

  const items: TimelineItemProps[] = [
    {
      color: 'green',
      key: 1,
      label: `Created on ${dayjs(workItem.created).format('MMM D, YYYY @ h:mm A')}`,
    },
  ]

  if (workItem.activatedTimestamp) {
    items.push({
      color: 'green',
      key: 2,
      label: `Activated on ${dayjs(workItem.activatedTimestamp).format('MMM D, YYYY @ h:mm A')}`,
    })
  }

  if (workItem.doneTimestamp) {
    items.push({
      color: 'green',
      key: 3,
      label: `Done on ${dayjs(workItem.doneTimestamp).format('MMM D, YYYY @ h:mm A')}`,
    })
  }

  // TODO: this doesn't look good
  return (
    <Card>
      <Timeline mode="right" items={items} />
    </Card>
  )
}

export default WorkItemTimeline
