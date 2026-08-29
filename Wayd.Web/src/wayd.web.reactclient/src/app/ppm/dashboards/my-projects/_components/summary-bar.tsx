'use client'

import { MetricCard } from '@/src/components/common/metrics'
import useTheme from '@/src/components/contexts/theme'
import { Col, Row, Skeleton } from 'antd'
import { FC, RefObject } from 'react'
import { useGetMyProjectsTaskMetricsQuery } from '@/src/store/features/ppm/projects-api'
import styles from '../my-projects-dashboard.module.css'

export interface MyProjectsSummaryBarProps {
  projectCount: number
  selectedStatuses: number[]
  selectedRoles: number[]
  isLoading: boolean
  containerRef?: RefObject<HTMLDivElement | null>
}

const MyProjectsSummaryBar: FC<MyProjectsSummaryBarProps> = ({
  projectCount,
  selectedStatuses,
  selectedRoles,
  isLoading,
  containerRef,
}) => {
  const { token } = useTheme()

  const queryArgs =
    selectedStatuses.length > 0 || selectedRoles.length > 0
      ? {
          status: selectedStatuses.length > 0 ? selectedStatuses : undefined,
          role: selectedRoles.length > 0 ? selectedRoles : undefined,
        }
      : undefined

  const { data: metrics, isLoading: metricsLoading } =
    useGetMyProjectsTaskMetricsQuery(queryArgs)

  if (isLoading) {
    return (
      <div ref={containerRef} className={styles.summaryBar}>
        <Skeleton active paragraph={{ rows: 1 }} />
      </div>
    )
  }

  return (
    <div ref={containerRef} className={styles.summaryBar}>
      <Row gutter={[8, 8]}>
        <Col xs={12} sm={6} md={4} lg={3}>
          <MetricCard title="Projects" value={projectCount} />
        </Col>
        <Col xs={12} sm={6} md={4} lg={3}>
          <MetricCard
            title="Overdue"
            value={metricsLoading ? '-' : (metrics?.overdue ?? 0)}
            valueStyle={
              (metrics?.overdue ?? 0) > 0
                ? { color: token.colorError }
                : undefined
            }
            tooltip="Sum of overdue tasks across visible projects"
          />
        </Col>
        <Col xs={12} sm={6} md={4} lg={3}>
          <MetricCard
            title="Due This Week"
            value={metricsLoading ? '-' : (metrics?.dueThisWeek ?? 0)}
            valueStyle={
              (metrics?.dueThisWeek ?? 0) > 0
                ? { color: token.colorWarning }
                : undefined
            }
            tooltip="Sum of tasks due this week across visible projects"
          />
        </Col>
        <Col xs={12} sm={6} md={4} lg={3}>
          <MetricCard
            title="Upcoming"
            value={metricsLoading ? '-' : (metrics?.upcoming ?? 0)}
            tooltip="Sum of tasks due next week across visible projects"
          />
        </Col>
      </Row>
    </div>
  )
}

export default MyProjectsSummaryBar
