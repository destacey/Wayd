'use client'

import { MetricCard } from '@/src/components/common/metrics'
import useTheme from '@/src/components/contexts/theme'
import { useGetProjectPlanSummaryQuery } from '@/src/store/features/ppm/projects-api'
import { Col, Row, Skeleton } from 'antd'
import { FC } from 'react'

export interface ProjectTaskMetricsProps {
  projectKey: string
  employeeId?: string
}

const ProjectTaskMetrics: FC<ProjectTaskMetricsProps> = ({
  projectKey,
  employeeId,
}) => {
  const { token } = useTheme()
  const { data: summary, isLoading } = useGetProjectPlanSummaryQuery({
    projectKey,
    employeeId,
  })

  if (isLoading) return <Skeleton active paragraph={{ rows: 1 }} />
  if (!summary || summary.totalLeafTasks === 0) return null

  return (
    <Row gutter={[8, 8]}>
      <Col xs={12} sm={8} md={8} lg={6}>
        <MetricCard
          title="Overdue"
          value={summary.overdue}
          valueStyle={
            summary.overdue > 0 ? { color: token.colorError } : undefined
          }
          tooltip="Tasks past their end date that are Not Started or In Progress."
        />
      </Col>
      <Col xs={12} sm={8} md={8} lg={6}>
        <MetricCard
          title="Due This Week"
          value={summary.dueThisWeek}
          valueStyle={
            summary.dueThisWeek > 0
              ? { color: token.colorWarning }
              : undefined
          }
          tooltip="Tasks due by Saturday that are Not Started or In Progress."
        />
      </Col>
      <Col xs={12} sm={8} md={8} lg={6}>
        <MetricCard
          title="Upcoming"
          value={summary.upcoming}
          tooltip="Tasks due next week that are Not Started or In Progress."
        />
      </Col>
    </Row>
  )
}

export default ProjectTaskMetrics
