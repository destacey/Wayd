'use client'

import {
  WorkItemDetailsDto,
  WorkItemProgressDailyRollupDto,
} from '@/src/services/wayd-api'
import WorkItemCycleTime from './work-item-cycle-time'
import WorkItemTimeToStart from './work-item-time-to-start'
import { Card, Space } from 'antd'
import WorkItemLeadTime from './work-item-lead-time'
import { WorkItemsCumulativeFlowChart } from '@/src/components/common/work'
import { useGetWorkItemMetricsQuery } from '@/src/store/features/work-management/workspace-api'
import WorkItemProgressPieChart from './work-item-progress-pie-chart'
import { WorkTypeTier } from '@/src/components/types'

export interface WorkItemDashboardProps {
  workItem: WorkItemDetailsDto
}

const WorkItemDashboard = ({ workItem }: WorkItemDashboardProps) => {
  const { data: metricsData, isLoading } = useGetWorkItemMetricsQuery(
    {
      idOrKey: workItem.workspace.key,
      workItemKey: workItem.key,
    },
    { skip: !workItem || workItem?.type.tier.id !== WorkTypeTier.Portfolio },
  )

  if (!workItem) return null

  // get the last item from the CFD data
  const progress: WorkItemProgressDailyRollupDto | null =
    metricsData && metricsData.length > 0
      ? metricsData[metricsData.length - 1]
      : null

  return (
    <>
      <Space align="start" wrap style={{ marginBottom: 10 }}>
        <WorkItemTimeToStart workItem={workItem} />
        <WorkItemCycleTime workItem={workItem} />
        <WorkItemLeadTime workItem={workItem} />
        {workItem?.type.tier.id === WorkTypeTier.Portfolio && progress && (
          <WorkItemProgressPieChart progress={progress} isLoading={isLoading} />
        )}
      </Space>
      {workItem?.type.tier.id === WorkTypeTier.Portfolio && metricsData && (
        <Card size="small">
          <WorkItemsCumulativeFlowChart
            workItems={metricsData}
            isLoading={isLoading}
          />
        </Card>
      )}
    </>
  )
}

export default WorkItemDashboard
