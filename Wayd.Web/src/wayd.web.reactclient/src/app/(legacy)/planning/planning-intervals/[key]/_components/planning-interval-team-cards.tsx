'use client'

import { CycleTimeMetric, MetricCard } from '@/src/components/common/metrics'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import { PlanningIntervalTeamMetrics } from '@/src/services/wayd-api'
import {
  useGetPlanningIntervalMetricsQuery,
  useGetPlanningIntervalObjectivesQuery,
} from '@/src/store/features/planning/planning-interval-api'
import {
  AimOutlined,
  CheckCircleOutlined,
  PlusCircleOutlined,
} from '@ant-design/icons'
import { Card, Col, Flex, Row, Skeleton, Typography } from 'antd'
import Link from 'next/link'
import { ObjectiveHealthChart, ObjectiveStatusChart } from '../../_components'

const { Text } = Typography

interface PlanningIntervalTeamCardsProps {
  piKey: number
}

interface TeamMetricsCardProps {
  piKey: number
  teamMetrics: PlanningIntervalTeamMetrics
}

const TeamMetricsCard = ({ piKey, teamMetrics }: TeamMetricsCardProps) => {
  const { data: objectivesData } = useGetPlanningIntervalObjectivesQuery(
    {
      planningIntervalKey: piKey,
      teamId: teamMetrics.team.id,
    },
    {
      skip: !piKey || !teamMetrics.team.id,
    },
  )

  const hasObjectives = !!objectivesData && objectivesData.length > 0
  const teamObjectiveCount =
    (teamMetrics.regularObjectivesCount ?? 0) +
    (teamMetrics.stretchObjectivesCount ?? 0)
  const hasTeamObjectives = teamObjectiveCount > 0

  return (
    <Link
      href={`/planning/planning-intervals/${piKey}/plan-review#${teamMetrics.teamCode.toLowerCase()}`}
      style={{ display: 'block', height: '100%', color: 'inherit' }}
    >
      <Card size="small" hoverable style={{ height: '100%' }}>
        <Flex vertical gap="small">
          <Text strong>{teamMetrics.team.name}</Text>
          <Flex vertical gap="large">
            <Row gutter={[8, 8]}>
              <Col span={12}>
                <MetricCard
                  embedded
                  title="Predictability"
                  value={
                    hasTeamObjectives
                      ? (teamMetrics.predictability ?? 0)
                      : 'N/A'
                  }
                  precision={hasTeamObjectives ? 0 : undefined}
                  suffix={hasTeamObjectives ? '%' : undefined}
                  tooltip="The team's predictability for this planning interval — completed objectives over committed (non-stretch) objectives. N/A means this metric is not applicable because the team has no objectives."
                  tooltipTarget="title"
                  secondaryValue={
                    hasTeamObjectives ? (
                      <Flex gap={12} style={{ fontSize: 12 }}>
                        <WaydTooltip title="Completed">
                          <span>
                            <CheckCircleOutlined
                              style={{ marginRight: 4 }}
                              aria-label="Completed"
                            />
                            {teamMetrics.completedObjectivesCount}
                          </span>
                        </WaydTooltip>
                        <WaydTooltip title="Regular (non-stretch)">
                          <span>
                            <AimOutlined
                              style={{ marginRight: 4 }}
                              aria-label="Regular"
                            />
                            {teamMetrics.regularObjectivesCount}
                          </span>
                        </WaydTooltip>
                        <WaydTooltip title="Stretch">
                          <span>
                            <PlusCircleOutlined
                              style={{ marginRight: 4 }}
                              aria-label="Stretch"
                            />
                            {teamMetrics.stretchObjectivesCount}
                          </span>
                        </WaydTooltip>
                      </Flex>
                    ) : undefined
                  }
                />
              </Col>
              <Col span={12}>
                <CycleTimeMetric
                  embedded
                  value={teamMetrics.cycleTime?.averageCycleTimeDays ?? 0}
                  tooltip="Average cycle time across this team's sprints in this planning interval."
                />
              </Col>
            </Row>
            {hasObjectives && (
              <Row gutter={[8, 8]}>
                <Col span={12}>
                  <ObjectiveStatusChart
                    objectivesData={objectivesData}
                    embedded
                    height={160}
                  />
                </Col>
                <Col span={12}>
                  <ObjectiveHealthChart
                    objectivesData={objectivesData}
                    embedded
                    height={160}
                  />
                </Col>
              </Row>
            )}
          </Flex>
        </Flex>
      </Card>
    </Link>
  )
}

const PlanningIntervalTeamCards = ({
  piKey,
}: PlanningIntervalTeamCardsProps) => {
  const { data, isLoading } = useGetPlanningIntervalMetricsQuery(piKey, {
    skip: !piKey,
  })

  if (isLoading) {
    return (
      <Card size="small" title="Teams">
        <Skeleton active paragraph={{ rows: 2 }} />
      </Card>
    )
  }

  const teamMetrics = data?.teamMetrics ?? []
  if (teamMetrics.length === 0) return null

  return (
    <Card size="small" title="Teams">
      <Row gutter={[16, 16]}>
        {teamMetrics.map((tm) => (
          <Col xs={24} md={12} lg={8} key={tm.team.id}>
            <TeamMetricsCard piKey={piKey} teamMetrics={tm} />
          </Col>
        ))}
      </Row>
    </Card>
  )
}

export default PlanningIntervalTeamCards

