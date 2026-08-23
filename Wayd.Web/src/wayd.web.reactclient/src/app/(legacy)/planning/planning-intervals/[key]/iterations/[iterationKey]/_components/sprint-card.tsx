'use client'

import {
  CompletionRateMetric,
  CycleTimeMetric,
  MetricCard,
  VelocityMetric,
} from '@/src/components/common/metrics'
import {
  IterationHealthIndicator,
  IterationProgressBar,
} from '@/src/components/common/planning'
import { IterationState } from '@/src/components/types'
import {
  SizingMethod,
  SprintMetricsSummary,
  TeamOperatingModelDetailsDto,
} from '@/src/services/wayd-api'
import { Card, Col, Flex, Grid, Row, Tag, Typography } from 'antd'
import { WaydTooltip } from '@/src/components/common'
import dayjs from 'dayjs'
import Link from 'next/link'
import { FC } from 'react'

const { Text } = Typography
const { useBreakpoint } = Grid

interface SprintCardProps {
  sprint: SprintMetricsSummary
  operatingModel?: TeamOperatingModelDetailsDto
  sizingMethod: SizingMethod
}

const SprintCard: FC<SprintCardProps> = ({
  sprint,
  operatingModel,
  sizingMethod,
}) => {
  const screens = useBreakpoint()
  const isXs = !screens.sm // xs screens (< 576px)

  const teamSupportsSP =
    operatingModel?.sizingMethod === SizingMethod.StoryPoints
  const useStoryPoints =
    sizingMethod === SizingMethod.StoryPoints && teamSupportsSP
  const effectiveSizingMethod = useStoryPoints
    ? SizingMethod.StoryPoints
    : SizingMethod.Count

  const displayTotal = useStoryPoints
    ? sprint.totalStoryPoints
    : sprint.totalWorkItems
  const displayCompleted = useStoryPoints
    ? sprint.completedStoryPoints
    : sprint.completedWorkItems
  const displayInProgress = useStoryPoints
    ? sprint.inProgressStoryPoints
    : sprint.inProgressWorkItems
  const displayNotStarted = useStoryPoints
    ? sprint.notStartedStoryPoints
    : sprint.notStartedWorkItems

  const formatDateRange = () => {
    const start = dayjs(sprint.start)
    const end = dayjs(sprint.end)
    return `${start.format('MMM D, YYYY h:mm A')} - ${end.format('MMM D, YYYY h:mm A')}`
  }

  const isFuture = sprint.state.id === IterationState.Future

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
              href={`/planning/sprints/${sprint.sprintKey}`}
              style={{ fontSize: 13, width: 'fit-content' }}
            >
              {sprint.sprintName}
            </Link>
            <Text type="secondary" style={{ fontSize: 12 }}>
              {formatDateRange()}
            </Text>
            <Flex gap={8} wrap>
              <IterationHealthIndicator
                startDate={new Date(sprint.start)}
                endDate={new Date(sprint.end)}
                total={displayTotal}
                completed={displayCompleted}
              />
              {sizingMethod === SizingMethod.StoryPoints && !teamSupportsSP && (
                <WaydTooltip title="This team does not support story point sizing. Values are based on work item counts.">
                  <Tag>Count-based Metrics</Tag>
                </WaydTooltip>
              )}
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
                href={`/planning/sprints/${sprint.sprintKey}`}
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
                startDate={new Date(sprint.start)}
                endDate={new Date(sprint.end)}
                total={displayTotal}
                completed={displayCompleted}
              />
              {sizingMethod === SizingMethod.StoryPoints && !teamSupportsSP && (
                <WaydTooltip title="This team does not support story point sizing. Values are based on work item counts.">
                  <Tag>Count-based Metrics</Tag>
                </WaydTooltip>
              )}
            </Flex>
          </Flex>
        )}

        {/* Progress Bar - only show for active/completed sprints */}
        {!isFuture && (
          <IterationProgressBar
            startDate={new Date(sprint.start)}
            endDate={new Date(sprint.end)}
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
                tooltip="Total story points or work items planned for this sprint"
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
                  tooltip="Total story points or work items currently in progress"
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
