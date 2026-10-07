'use client'

import {
  CompletionRateMetric,
  CycleTimeMetric,
  MetricCard,
  sprintOverviewFigures,
} from '@/src/components/common/metrics'
import {
  IterationHealthIndicator,
  IterationProgressBar,
  SprintSayDoMetric,
} from '@/src/components/common/planning'
import useTheme from '@/src/components/contexts/theme'
import { useGetSprintScopeQuery } from '@/src/store/features/work-management/sprints-api'
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
  const isFuture = sprint.state.id === IterationState.Future
  const isCompleted = sprint.state.id === IterationState.Completed
  const { token } = useTheme()

  // A started sprint is measured on its scope, as on the sprint page and Home:
  // velocity and completion keep carried-over work, and health is measured
  // against the commitment.
  const { data: scope } = useGetSprintScopeQuery(sprint.sprintKey, {
    skip: isFuture,
  })
  const figures = sprintOverviewFigures(sprint, scope, byCount)
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
                total={figures.completionBase}
                completed={figures.completed}
                committed={figures.scope?.committed}
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
                total={figures.completionBase}
                completed={figures.completed}
                committed={figures.scope?.committed}
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
            total={figures.completionBase}
            completed={figures.completed}
          />
        )}

        {/* Metrics Row */}
        <Row gutter={[8, 8]}>
          {isFuture ? (
            <Col xs={12} sm={8} md={6}>
              <MetricCard
                title="Total"
                value={figures.currentTotal}
                tooltip={`Total ${measure} planned for this sprint`}
                cardStyle={metricCardStyle}
              />
            </Col>
          ) : (
            <>
              <Col xs={12} sm={8} md={6}>
                {figures.scope?.predictability != null ? (
                  <MetricCard
                    title="Predictability"
                    value={figures.scope.predictability * 100}
                    precision={0}
                    suffix="%"
                    tooltip={`Velocity ÷ the ${measure} committed at the commitment point, up to 100%.`}
                    cardStyle={metricCardStyle}
                  />
                ) : (
                  // Before the sprint has a commitment there is nothing to divide by.
                  <CompletionRateMetric
                    completed={figures.completed}
                    total={figures.completionBase}
                    cardStyle={metricCardStyle}
                    tooltip={effectiveSizingMethod}
                  />
                )}
              </Col>
              <Col xs={12} sm={8} md={6}>
                <MetricCard
                  title="Velocity"
                  value={figures.completed}
                  valueStyle={{ color: token.colorSuccess }}
                  tooltip={`The ${measure} completed while in the sprint. Work moved to a Removed status in the sprint counts too.`}
                  cardStyle={metricCardStyle}
                />
              </Col>
              <Col xs={12} sm={8} md={6}>
                {/* Once the sprint has ended, its unfinished work is carried over, not in progress. */}
                {isCompleted && figures.scope ? (
                  <MetricCard
                    title="Carried Over"
                    value={figures.scope.carriedOver}
                    tooltip={`Unfinished ${measure} still in the sprint at its end, or moved to the team's next sprint on its last day.`}
                    cardStyle={metricCardStyle}
                  />
                ) : (
                  <MetricCard
                    title="In Progress"
                    value={figures.inProgress}
                    secondaryValue={`${figures.notStarted} not started`}
                    tooltip={`The ${measure} in the sprint now that are in progress`}
                    cardStyle={metricCardStyle}
                  />
                )}
              </Col>
              <Col xs={12} sm={8} md={6}>
                {/* Until the sprint has a say/do ratio, its cycle time fills the slot. */}
                {scope && figures.scope?.sayDo != null ? (
                  <SprintSayDoMetric
                    scope={scope}
                    byCount={byCount}
                    cardStyle={metricCardStyle}
                  />
                ) : (
                  <CycleTimeMetric
                    value={sprint.cycleTime?.averageCycleTimeDays ?? 0}
                    cardStyle={metricCardStyle}
                  />
                )}
              </Col>
            </>
          )}
        </Row>
      </Flex>
    </Card>
  )
}

export default SprintCard
