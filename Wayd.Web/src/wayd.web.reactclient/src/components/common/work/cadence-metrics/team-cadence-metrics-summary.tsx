'use client'

import { Flex, Typography } from 'antd'
import { FC } from 'react'
import { SizingMethod } from '@/src/services/wayd-api'
import { useGetTeamCadenceMetricsQuery } from '@/src/store/features/organizations/team-api'
import { sizingMethodMeasure } from '@/src/utils'
import { METRIC_CARD_FLEX, MetricCard } from '../../metrics'
import {
  formatVelocityAverage,
  formatVelocityRange,
  periodCountText,
  VELOCITY_TOOLTIP,
} from './team-cadence-metrics-formatting'

const { Text } = Typography

export interface TeamCadenceMetricsSummaryProps {
  teamId: string
  /** Opens the team's velocity report, which lists the periods behind the average. */
  onOpenReport?: () => void
}

/**
 * A Kanban team's average velocity over the organization's window of its
 * cadence periods, in the team's sizing method. Renders nothing for a team
 * with no ended period to measure, so a page showing it carries no empty
 * section.
 */
const TeamCadenceMetricsSummary: FC<TeamCadenceMetricsSummaryProps> = ({
  teamId,
  onOpenReport,
}) => {
  const { data: metrics, isLoading } = useGetTeamCadenceMetricsQuery(
    { teamId },
    { skip: !teamId },
  )

  if (!isLoading && (!metrics || !metrics.byCount)) return null

  // With no averaged period yet in the team's sizing method there is nothing to
  // average in it, so the periods are counted by work item instead.
  const showsCount =
    metrics?.sizingMethod === SizingMethod.Count || !metrics?.byEstimate
  const summary =
    (showsCount ? metrics?.byCount : metrics?.byEstimate) ?? undefined
  const measure = sizingMethodMeasure(
    showsCount
      ? SizingMethod.Count
      : (metrics?.sizingMethod ?? SizingMethod.Count),
  )

  return (
    <Flex vertical gap="small">
      <Flex gap="small" align="baseline" wrap>
        <Text strong style={{ fontSize: 14 }}>
          Velocity
        </Text>
        {summary && (
          <Text type="secondary">{`Last ${periodCountText(summary.periods)}`}</Text>
        )}
      </Flex>
      <Flex gap="small" wrap>
        <MetricCard
          title="Average Velocity"
          value={formatVelocityAverage(summary)}
          secondaryValue={formatVelocityRange(summary)}
          tooltip={`${VELOCITY_TOOLTIP} Measured in ${measure}${summary ? `, over ${periodCountText(summary.periods)}` : ''}.`}
          loading={isLoading}
          cardStyle={METRIC_CARD_FLEX}
          onClick={onOpenReport}
          ariaLabel="Average Velocity: open the velocity report"
        />
      </Flex>
    </Flex>
  )
}

export default TeamCadenceMetricsSummary
