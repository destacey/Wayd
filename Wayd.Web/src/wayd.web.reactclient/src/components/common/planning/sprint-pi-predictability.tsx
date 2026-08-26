'use client'

import { MetricCard } from '../metrics'
import WaydTooltip from '../wayd-tooltip'
import { useGetSprintPlanningIntervalsQuery } from '@/src/store/features/planning/sprints-api'
import { useGetPlanningIntervalMetricsQuery } from '@/src/store/features/planning/planning-interval-api'
import { NavigationDto } from '@/src/services/wayd-api'
import { Col, Row } from 'antd'
import Link from 'next/link'
import { FC } from 'react'
import {
  AimOutlined,
  CheckCircleOutlined,
  PlusCircleOutlined,
} from '@ant-design/icons'

interface PiPredictabilityCardProps {
  pi: NavigationDto
  teamId: string
}

const PiPredictabilityCard: FC<PiPredictabilityCardProps> = ({ pi, teamId }) => {
  const { data: metrics } = useGetPlanningIntervalMetricsQuery(pi.key, { skip: !pi.key })

  const teamMetrics = metrics?.teamMetrics?.find((tm) => tm.team.id === teamId)
  const hasTeamObjectives = (teamMetrics?.regularObjectivesCount ?? 0) + (teamMetrics?.stretchObjectivesCount ?? 0) > 0

  if (!teamMetrics || !hasTeamObjectives) return null

  const href = `/planning/planning-intervals/${pi.key}?section=plan-review&team=${teamMetrics.teamCode.toLowerCase()}`

  return (
    <Link href={href} style={{ display: 'block' }}>
    <MetricCard
      hoverable
      title={`Predictability - ${pi.name}`}
      value={teamMetrics?.predictability ?? 0}
      precision={0}
      suffix="%"
      tooltip="The team's predictability for this planning interval — completed objectives over committed (non-stretch) objectives."
      tooltipTarget="title"
      secondaryValue={
        teamMetrics ? (
          <span style={{ display: 'flex', gap: 12, fontSize: 12 }}>
            <WaydTooltip title="Completed">
              <span>
                <CheckCircleOutlined style={{ marginRight: 4 }} />
                {teamMetrics.completedObjectivesCount}
              </span>
            </WaydTooltip>
            <WaydTooltip title="Regular (non-stretch)">
              <span>
                <AimOutlined style={{ marginRight: 4 }} />
                {teamMetrics.regularObjectivesCount}
              </span>
            </WaydTooltip>
            <WaydTooltip title="Stretch">
              <span>
                <PlusCircleOutlined style={{ marginRight: 4 }} />
                {teamMetrics.stretchObjectivesCount}
              </span>
            </WaydTooltip>
          </span>
        ) : undefined
      }
    />
    </Link>
  )
}

interface SprintPiPredictabilityProps {
  sprintKey: number
  teamId: string
}

const SprintPiPredictability: FC<SprintPiPredictabilityProps> = ({ sprintKey, teamId }) => {
  const { data: planningIntervals } = useGetSprintPlanningIntervalsQuery(sprintKey, {
    skip: !sprintKey,
  })

  if (!planningIntervals || planningIntervals.length === 0) return null

  return (
    <Row gutter={[8, 8]}>
      {planningIntervals.map((pi) => (
        <Col key={pi.id} xs={24}>
          <PiPredictabilityCard pi={pi} teamId={teamId} />
        </Col>
      ))}
    </Row>
  )
}

export default SprintPiPredictability
