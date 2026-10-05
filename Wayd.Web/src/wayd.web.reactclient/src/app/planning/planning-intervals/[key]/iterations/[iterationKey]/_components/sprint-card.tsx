'use client'

import {
  CompletionRateMetric,
  CycleTimeMetric,
  MetricCard,
  VelocityMetric,
  sprintMetricValues,
} from '@/src/components/common/metrics'
import {
  IterationHealthIndicator,
  IterationProgressBar,
} from '@/src/components/common/planning'
import { IterationState } from '@/src/components/types'
import { SizingMethod, SprintMetricsSummary } from '@/src/services/wayd-api'
import { Card, Col, Flex, Grid, Row, Tag, Typography } from 'antd'
import { WaydTooltip } from '@/src/components/common'
import {
  formatCalendarDate,
  sizingMethodLabel,
  sizingMethodMeasure,
  sprintActiveDays,
} from '@/src/utils'
import Link from 'next/link'
import { FC } from 'react'

const { Text } = Typography
const { useBreakpoint } = Grid

interface SprintCardProps {
  sprint: SprintMetricsSummary
  /** Counts work items rather than summing the sprint's own sizing method. */
  byCount: boolean
}

const SprintCard: FC<SprintCardProps> = ({ sprint, byCount }) => {
  const screens = useBreakpoint()
  const isXs = !screens.sm // xs screens (< 576px)

  // Each team's sprint is shown in its own sizing method; units are never mixed across cards.
  const effectiveSizingMethod = byCount
    ? SizingMethod.Count
    : sprint.sizingMethod
  const {
    total: displayTotal,
    completed: displayCompleted,
    inProgress: displayInProgress,
    notStarted: displayNotStarted,
  } = sprintMetricValues(sprint, byCount)
  const measure = sizingMethodMeasure(effectiveSizingMethod)

  const unitTag = (
    <WaydTooltip
      title={`This team's sprint is measured in ${measure}: its sizing method on the sprint's planned start.`}
    >
      <Tag>{sizingMethodLabel(effectiveSizingMethod)}</Tag>
    </WaydTooltip>
  )

  const formatDateRange = () =>
    `${formatCalendarDate(sprint.start)} - ${formatCalendarDate(sprint.end)}`

  const isFuture = sprint.state.id === IterationState.Future
  const activeDays = sprintActiveDays(sprint)

  const metricCardStyle: React.CSSProperties = {
    height: '100%',
  }

  return (
    <Card
      size="small"
      hoverable
      styles={{
        body: { padding: 16 },
      }}
    >
      <Flex vertical gap="middle">
        {/* Header */}
        {isXs ? (
          <Flex vertical gap={8}>
            <Link
              href={`/organizations/teams/${sprint.team.key}`}
              style={{ fontSize: 16, fontWeight: 600, width: 'fit-content' }}
            >
              {sprint.team.name}
            </Link>
            <Link
              href={`/work/sprints/${sprint.sprintKey}`}
              style={{ fontSize: 13, width: 'fit-content' }}
            >
              {sprint.sprintName}
            </Link>
            <Text type="secondary" style={{ fontSize: 12 }}>
              {formatDateRange()}
            </Text>
            <Flex gap={8} wrap>
              <IterationHealthIndicator
                startDate={activeDays.start}
                endDate={activeDays.end}
                total={displayTotal}
                completed={displayCompleted}
              />
              {unitTag}
            </Flex>
          </Flex>
        ) : (
          <Flex justify="space-between" align="flex-start">
            <Flex vertical gap={2}>
              <Link
                href={`/organizations/teams/${sprint.team.key}`}
                style={{ fontSize: 16, fontWeight: 600, width: 'fit-content' }}
              >
                {sprint.team.name}
              </Link>
              <Link
                href={`/work/sprints/${sprint.sprintKey}`}
                style={{ fontSize: 13, width: 'fit-content' }}
              >
                {sprint.sprintName}
              </Link>
              <Text type="secondary" style={{ fontSize: 12 }}>
                {formatDateRange()}
              </Text>
            </Flex>

            <Flex vertical gap={8} align="end">
              <IterationHealthIndicator
                startDate={activeDays.start}
                endDate={activeDays.end}
                total={displayTotal}
                completed={displayCompleted}
              />
              {unitTag}
            </Flex>
          </Flex>
        )}

        {/* Progress Bar - only show for active/completed sprints */}
        {!isFuture && (
          <IterationProgressBar
            startDate={activeDays.start}
            endDate={activeDays.end}
            total={displayTotal}
            completed={displayCompleted}
          />
        )}

        {/* Metrics Row */}
        <Row gutter={[8, 8]}>
          {isFuture ? (
            <Col xs={12} sm={8} md={6}>
              <MetricCard
                title="Total"
                value={displayTotal}
                tooltip={`Total ${measure} planned for this sprint`}
                cardStyle={metricCardStyle}
              />
            </Col>
          ) : (
            <>
              <Col xs={12} sm={8} md={6}>
                <CompletionRateMetric
                  completed={displayCompleted}
                  total={displayTotal}
                  cardStyle={metricCardStyle}
                  tooltip={effectiveSizingMethod}
                />
              </Col>
              <Col xs={12} sm={8} md={6}>
                <VelocityMetric
                  completed={displayCompleted}
                  total={displayTotal}
                  cardStyle={metricCardStyle}
                  tooltip={effectiveSizingMethod}
                />
              </Col>
              <Col xs={12} sm={8} md={6}>
                <MetricCard
                  title="In Progress"
                  value={displayInProgress}
                  secondaryValue={`${displayNotStarted} not started`}
                  tooltip={`Total ${measure} currently in progress`}
                  cardStyle={metricCardStyle}
                />
              </Col>
              <Col xs={12} sm={8} md={6}>
                <CycleTimeMetric
                  value={sprint.cycleTime?.averageCycleTimeDays ?? 0}
                  cardStyle={metricCardStyle}
                />
              </Col>
            </>
          )}
        </Row>
      </Flex>
    </Card>
  )
}

export default SprintCard
