'use client'

import { useGetActiveSprintQuery } from '@/src/store/features/organizations/team-api'
import {
  useGetSprintMetricsQuery,
  useGetSprintScopeQuery,
} from '@/src/store/features/work-management/sprints-api'
import { SizingMethod } from '@/src/services/wayd-api'
import { Card, Col, Flex, Row, Skeleton, Typography } from 'antd'
import Link from 'next/link'
import { FC } from 'react'
import {
  CompletionRateMetric,
  CycleTimeMetric,
  MetricCard,
  StatusMetric,
  sprintOverviewFigures,
} from '../metrics'
import SprintSayDoMetric from './sprint-say-do-metric'
import useTheme from '@/src/components/contexts/theme'
import SprintPiPredictability from './sprint-pi-predictability'
import TimelineProgress from './timeline-progress'
import IterationHealthIndicator from './iteration-health-indicator'
import { sizingMethodMeasure, sprintActiveDays } from '@/src/utils'

const { Text } = Typography

export interface ActiveTeamSprintProps {
  teamId: string
  showTeamLink?: boolean
}

const ActiveTeamSprint: FC<ActiveTeamSprintProps> = ({
  teamId,
  showTeamLink = false,
}) => {
  const { token } = useTheme()

  const { data: sprintData, isLoading: sprintIsLoading } =
    useGetActiveSprintQuery(teamId)

  const sprintKey = sprintData?.key
  const { data: metrics, isLoading: metricsIsLoading } =
    useGetSprintMetricsQuery(sprintKey!, {
      skip: !sprintKey,
    })

  const { data: scope, isLoading: scopeIsLoading } = useGetSprintScopeQuery(
    sprintKey!,
    { skip: !sprintKey },
  )

  // Shown in the sizing method the sprint is measured in, which the metrics report.
  const sizingMethod = metrics?.sizingMethod ?? SizingMethod.Count
  const measure = sizingMethodMeasure(sizingMethod)
  const figures = sprintOverviewFigures(metrics, scope, false)

  if (sprintIsLoading) {
    return <Skeleton active paragraph={{ rows: 3 }} />
  }

  if (!sprintData) {
    return null
  }

  const activeDays = sprintActiveDays(sprintData)

  const title = (
    <Flex justify="space-between">
      <div>
        {showTeamLink ? (
          <>
            <Link href={`/organizations/teams/${sprintData.team.key}`}>
              {sprintData.team.code}
            </Link>
            <Text> · </Text>
          </>
        ) : (
          <Text>Active Sprint: </Text>
        )}
        <Link href={`/work/sprints/${sprintData.key}`}>{sprintData.name}</Link>
      </div>
      <IterationHealthIndicator
        startDate={activeDays.start}
        endDate={activeDays.end}
        total={figures.completionBase}
        completed={figures.completed}
      />
    </Flex>
  )

  return (
    <Card
      title={title}
      size="small"
      loading={metricsIsLoading || scopeIsLoading}
    >
      <Flex vertical gap="small">
        <TimelineProgress
          start={activeDays.start}
          end={activeDays.end}
          variant="borderless"
          size="small"
          style={{ width: '100%' }}
        />
        <Row gutter={[8, 8]}>
          <Col xs={12}>
            {figures.scope?.predictability != null ? (
              <MetricCard
                title="Predictability"
                value={figures.scope.predictability * 100}
                precision={0}
                suffix="%"
                tooltip={`Velocity ÷ the ${measure} committed at the commitment point, up to 100%.`}
              />
            ) : (
              // Before the sprint has a commitment there is nothing to divide by.
              <CompletionRateMetric
                completed={figures.completed}
                total={figures.completionBase}
                tooltip={sizingMethod}
              />
            )}
          </Col>
          <Col xs={12}>
            <MetricCard
              title="Velocity"
              value={figures.completed}
              valueStyle={{ color: token.colorSuccess }}
              tooltip={`The ${measure} completed while in the sprint. Unlike most tools' velocity, work moved to a Removed status in the sprint counts too.`}
            />
          </Col>
          <Col xs={12}>
            <StatusMetric
              title="In Progress"
              value={figures.inProgress}
              total={figures.currentTotal}
              color={token.colorInfo}
              tooltip={`The ${measure} in the sprint now that are in progress (Status Category: Active). The percentage is their share of the sprint's work now.`}
            />
          </Col>
          <Col xs={12}>
            {/* Until the sprint has a say/do ratio, its cycle time fills the slot. */}
            {scope && figures.scope?.sayDo != null ? (
              <SprintSayDoMetric scope={scope} />
            ) : (
              <CycleTimeMetric
                value={metrics?.cycleTime?.averageCycleTimeDays ?? 0}
              />
            )}
          </Col>
        </Row>
        <SprintPiPredictability
          sprintKey={sprintData.key}
          teamId={sprintData.team.id}
        />
      </Flex>
    </Card>
  )
}

export default ActiveTeamSprint
