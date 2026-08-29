'use client'

import { WorkItemDetailsDto } from '@/src/services/wayd-api'
import {
  METRIC_CARD_FLEX,
  MetricCard,
} from '@/src/components/common/metrics'
import dayjs from 'dayjs'

export interface WorkItemTimeToStartProps {
  workItem: WorkItemDetailsDto
}

const WorkItemTimeToStart = ({ workItem }: WorkItemTimeToStartProps) => {
  if (!workItem) return null

  let metricName
  let metricValue
  let tooltip
  switch (workItem.statusCategory.name) {
    case 'Proposed':
      metricName = 'Time Proposed'
      metricValue = dayjs().diff(dayjs(workItem.created), 'day', true)
      tooltip =
        'Total time the work item has been in the proposed status category. Time Proposed = now - created'
      break
    case 'Active':
    case 'Done':
      metricName = 'Time to Start'
      metricValue = dayjs(workItem.activatedTimestamp).diff(
        dayjs(workItem.created),
        'day',
        true,
      )
      tooltip =
        'Total time the work item took to become active. Time to Start = activated - created'
      break
    default:
      metricName = 'Unknown'
  }

  return (
    <MetricCard
      title={metricName}
      value={metricValue}
      suffix="days"
      precision={2}
      tooltip={tooltip}
      cardStyle={METRIC_CARD_FLEX}
    />
  )
}

export default WorkItemTimeToStart
