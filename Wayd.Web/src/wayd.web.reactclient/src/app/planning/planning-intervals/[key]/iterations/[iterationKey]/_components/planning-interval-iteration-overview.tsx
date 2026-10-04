'use client'

import {
  CompletionRateMetric,
  CycleTimeMetric,
  MetricCard,
  StatusMetric,
  VelocityMetric,
} from '@/src/components/common/metrics'
import TimelineProgress from '@/src/components/common/planning/timeline-progress'
import useTheme from '@/src/components/contexts/theme'
import { PlanningIntervalIterationDetailsDto } from '@/src/services/wayd-api'
import { useGetPlanningIntervalIterationMetricsQuery } from '@/src/store/features/planning/planning-interval-api'
import {
  Col,
  Divider,
  Flex,
  Row,
  Segmented,
  Skeleton,
  Tooltip,
  Typography,
} from 'antd'
import { FC, ReactNode, useEffect, useState } from 'react'
import { SprintCard } from '.'
import { IterationHealthIndicator } from '@/src/components/common/planning'

const TEAM_SIZING = 'Team Sizing'
const COUNT = 'Count'

const { Title } = Typography

export interface PlanningIntervalIterationOverviewProps {
  iteration: PlanningIntervalIterationDetailsDto
  onHealthIndicatorReady?: (indicator: ReactNode) => void
}

const PlanningIntervalIterationOverview: FC<
  PlanningIntervalIterationOverviewProps
> = ({ iteration, onHealthIndicatorReady }) => {
  const [byCount, setByCount] = useState(false)
  const { token } = useTheme()

  const { data: metrics, isLoading } =
    useGetPlanningIntervalIterationMetricsQuery({
      planningIntervalKey: iteration.planningInterval.key,
      iterationKey: iteration.key,
    })

  const sortedSprints = !metrics
    ? []
    : [...metrics.sprintMetrics].sort((a, b) =>
        a.team.name.localeCompare(b.team.name),
      )

  const displayValues = !metrics
    ? {
        total: 0,
        completed: 0,
        inProgress: 0,
        notStarted: 0,
      }
    : {
        total: metrics.totalWorkItems,
        completed: metrics.completedWorkItems,
        inProgress: metrics.inProgressWorkItems,
        notStarted: metrics.notStartedWorkItems,
      }

  // Notify parent when health indicator is ready
  useEffect(() => {
    if (!isLoading && metrics && onHealthIndicatorReady) {
      onHealthIndicatorReady(
        <IterationHealthIndicator
          startDate={iteration.start}
          endDate={iteration.end}
          total={displayValues.total}
          completed={displayValues.completed}
        />,
      )
    }
  }, [
    displayValues.completed,
    displayValues.total,
    isLoading,
    metrics,
    onHealthIndicatorReady,
    iteration.end,
    iteration.start,
  ])

  if (isLoading || !metrics) {
    return <Skeleton active />
  }

  const isFuture = iteration.state === 'Future'

  return (
    <Flex vertical gap="middle">
      <TimelineProgress
        start={iteration.start}
        end={iteration.end}
        dateFormat="MMM D"
      />

      <Row gutter={[8, 8]}>
        <Col xs={12} sm={8} md={6} lg={4} xxl={3}>
          <MetricCard
            title="Team Sprints"
            value={metrics.teamCount}
            tooltip="Number of teams with sprints mapped to this iteration."
          />
        </Col>
        {!isFuture && (
          <Col xs={12} sm={8} md={6} lg={4} xxl={3}>
            <CompletionRateMetric
              completed={displayValues.completed}
              total={displayValues.total}
              tooltip="Percentage of work items across all team sprints that are completed (Done or Removed)."
            />
          </Col>
        )}
        <Col xs={12} sm={8} md={6} lg={4} xxl={3}>
          <MetricCard
            title="Total"
            value={displayValues.total}
            tooltip="Total number of work items across all team sprints in this iteration."
          />
        </Col>
        {!isFuture && (
          <>
            <Col xs={12} sm={8} md={6} lg={4} xxl={3}>
              <VelocityMetric
                completed={displayValues.completed}
                total={displayValues.total}
                tooltip="Total completed work items across all team sprints (Done or Removed)."
              />
            </Col>
            <Col xs={12} sm={8} md={6} lg={4} xxl={3}>
              <StatusMetric
                title="In Progress"
                value={displayValues.inProgress}
                total={displayValues.total}
                color={token.colorInfo}
                tooltip="Total in-progress work items across all team sprints."
              />
            </Col>
            <Col xs={12} sm={8} md={6} lg={4} xxl={3}>
              <StatusMetric
                title="Not Started"
                value={displayValues.notStarted}
                total={displayValues.total}
                tooltip="Total not-started work items across all team sprints."
              />
            </Col>
            {metrics.cycleTime && metrics.cycleTime.workItemsCount > 0 && (
              <Col xs={12} sm={8} md={6} lg={4} xxl={3}>
                <CycleTimeMetric
                  value={metrics.cycleTime.averageCycleTimeDays ?? 0}
                  tooltip="Average cycle time of completed (Done) work items across all team sprints."
                />
              </Col>
            )}
          </>
        )}
      </Row>

      <Divider />

      {sortedSprints.length > 0 && (
        <Flex vertical gap="small">
          <Flex gap="small" justify="space-between">
            <Title level={5} style={{ margin: 0 }}>
              Team Sprints
            </Title>
            <Tooltip title="Switch between showing each team's sprint in its own sizing method and counting work items">
              <Segmented<string>
                options={[TEAM_SIZING, COUNT]}
                value={byCount ? COUNT : TEAM_SIZING}
                onChange={(value) => setByCount(value === COUNT)}
              />
            </Tooltip>
          </Flex>
          <Flex vertical gap={8}>
            {sortedSprints.map((sprint) => (
              <SprintCard
                key={sprint.sprintId}
                sprint={sprint}
                byCount={byCount}
              />
            ))}
          </Flex>
        </Flex>
      )}
    </Flex>
  )
}

export default PlanningIntervalIterationOverview
