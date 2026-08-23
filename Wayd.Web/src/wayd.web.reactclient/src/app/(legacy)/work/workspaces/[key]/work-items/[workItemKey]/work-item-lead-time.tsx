'use client'

import { WorkItemDetailsDto } from '@/src/services/wayd-api'
import { Card, Statistic } from 'antd'
import { WaydTooltip } from '@/src/components/common'
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
    <Card>
      <WaydTooltip title={tooltip}>
        <Statistic
          title={metricName}
          value={metricValue}
          suffix="days"
          precision={2}
        />
      </WaydTooltip>
    </Card>
  )
}

export default WorkItemLeadTime
