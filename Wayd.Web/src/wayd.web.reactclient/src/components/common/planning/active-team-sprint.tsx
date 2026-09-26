'use client'

import { useGetActiveSprintQuery, useGetTeamDetailsQuery } from '@/src/store/features/organizations/team-api'
import { useGetSprintMetricsQuery } from '@/src/store/features/planning/sprints-api'
import { SizingMethod } from '@/src/services/wayd-api'
import { Card, Col, Flex, Row, Skeleton, Typography } from 'antd'
import Link from 'next/link'
import { FC } from 'react'
import { CompletionRateMetric, CycleTimeMetric, StatusMetric, VelocityMetric } from '../metrics'
import useTheme from '@/src/components/contexts/theme'
import SprintPiPredictability from './sprint-pi-predictability'
import TimelineProgress from './timeline-progress'
import IterationHealthIndicator from './iteration-health-indicator'

const { Text } = Typography

export interface ActiveTeamSprintProps {
  teamId: string
  sizingMethod?: SizingMethod
  showTeamLink?: boolean
}

const ActiveTeamSprint: FC<ActiveTeamSprintProps> = ({
  teamId,
  sizingMethod,
  showTeamLink = false,
}) => {
  const { token } = useTheme()

  const { data: sprintData, isLoading: sprintIsLoading } =
    useGetActiveSprintQuery(teamId)

  const { data: teamDetails } = useGetTeamDetailsQuery(sprintData?.team.key ?? 0, {
    skip: sizingMethod !== undefined || !sprintData?.team.key,
  })

  const resolvedSizingMethod = sizingMethod ?? teamDetails?.operatingModel?.sizingMethod ?? SizingMethod.StoryPoints
  const useStoryPoints = resolvedSizingMethod === SizingMethod.StoryPoints

  const sprintKey = sprintData?.key
  const { data: metrics, isLoading: metricsIsLoading } =
    useGetSprintMetricsQuery(sprintKey!, {
      skip: !sprintKey,
    })

  const displayValues = (() => {
    if (!metrics) return { total: 0, completed: 0, inProgress: 0, notStarted: 0 }
    return {
      total: useStoryPoints ? metrics.totalStoryPoints : metrics.totalWorkItems,
      completed: useStoryPoints ? metrics.completedStoryPoints : metrics.completedWorkItems,
      inProgress: useStoryPoints ? metrics.inProgressStoryPoints : metrics.inProgressWorkItems,
      notStarted: useStoryPoints ? metrics.notStartedStoryPoints : metrics.notStartedWorkItems,
    }
  })()

  if (sprintIsLoading) {
    return <Skeleton active paragraph={{ rows: 3 }} />
  }

  if (!sprintData) {
    return null
  }

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
        <Link href={`/planning/sprints/${sprintData.key}`}>
          {sprintData.name}
        </Link>
      </div>
      <IterationHealthIndicator
        startDate={new Date(sprintData.start)}
        endDate={new Date(sprintData.end)}
        total={displayValues.total}
        completed={displayValues.completed}
      />
    </Flex>
  )

  return (
    <Card title={title} size="small" loading={metricsIsLoading}>
      <Flex vertical gap="small">
        <TimelineProgress
          start={sprintData.start}
          end={sprintData.end}
          variant="borderless"
          size="small"
          style={{ width: '100%' }}
        />
        <Row gutter={[8, 8]}>
          <Col xs={12}>
            <CompletionRateMetric
              completed={displayValues.completed}
              total={displayValues.total}
              tooltip={resolvedSizingMethod}
            />
          </Col>
          <Col xs={12}>
            <VelocityMetric
              completed={displayValues.completed}
              total={displayValues.total}
              tooltip={resolvedSizingMethod}
            />
          </Col>
          <Col xs={12}>
            <StatusMetric
              title="In Progress"
              value={displayValues.inProgress}
              total={displayValues.total}
              color={token.colorInfo}
              tooltip="Total number of story points or items currently in the sprint that are in progress (Status Category: Active). Percentage shown represents the portion of total sprint work that is in progress."
            />
          </Col>
          <Col xs={12}>
            <CycleTimeMetric
              value={metrics?.cycleTime?.averageCycleTimeDays ?? 0}
            />
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
