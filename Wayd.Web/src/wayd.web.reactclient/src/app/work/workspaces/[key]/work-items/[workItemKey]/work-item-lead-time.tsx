'use client'

import { WorkItemDetailsDto } from '@/src/services/wayd-api'
import {
  METRIC_CARD_FLEX,
  MetricCard,
} from '@/src/components/common/metrics'
import dayjs from 'dayjs'

export interface WorkItemLeadTimeProps {
  workItem: WorkItemDetailsDto
}

const WorkItemLeadTime = ({ workItem }: WorkItemLeadTimeProps) => {
  if (!workItem || workItem.statusCategory.name !== 'Done') return null

  const metricName = 'Lead Time'
  const metricValue = dayjs(workItem.doneTimestamp).diff(
    dayjs(workItem.created),
    'day',
    true,
  )
  const tooltip =
    'Total time the work item was in the system. Lead Time = completed - created'

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

export default WorkItemLeadTime
