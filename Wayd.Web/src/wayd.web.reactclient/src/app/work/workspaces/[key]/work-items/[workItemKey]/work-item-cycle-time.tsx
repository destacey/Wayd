'use client'

import { WorkItemDetailsDto } from '@/src/services/wayd-api'
import {
  METRIC_CARD_FLEX,
  MetricCard,
} from '@/src/components/common/metrics'
import dayjs from 'dayjs'

export interface WorkItemCycleTimeProps {
  workItem: WorkItemDetailsDto
}

const WorkItemCycleTime = ({ workItem }: WorkItemCycleTimeProps) => {
  if (!workItem || workItem.statusCategory.name === 'Proposed') return null

  let metricName
  let metricValue
  let tooltip
  switch (workItem.statusCategory.name) {
    case 'Active':
      metricName = 'Time Active'
      metricValue = dayjs().diff(
        dayjs(workItem.activatedTimestamp),
        'day',
        true,
      )
      tooltip =
        'Total time the work item has been in the active status category. Time Active = now - activated'
      break
    case 'Done':
      metricName = 'Cycle Time'
      metricValue = dayjs(workItem.doneTimestamp).diff(
        dayjs(workItem.activatedTimestamp),
        'day',
        true,
      )
      tooltip =
        'Total time the work item was in the active status category. Cycle Time = completed - activated'
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

export default WorkItemCycleTime
