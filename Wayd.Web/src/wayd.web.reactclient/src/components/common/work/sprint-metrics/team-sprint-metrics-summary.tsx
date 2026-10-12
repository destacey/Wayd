'use client'

import { Flex, Typography } from 'antd'
import { FC } from 'react'
import { SizingMethod } from '@/src/services/wayd-api'
import { useGetTeamSprintMetricsQuery } from '@/src/store/features/organizations/team-api'
import { sizingMethodMeasure } from '@/src/utils'
import { METRIC_CARD_FLEX, MetricCard } from '../../metrics'
import {
  formatSprintMetricAverage,
  formatSprintMetricRange,
  METRIC_TOOLTIPS,
  sprintCountText,
  SprintMetricKind,
  sprintMetricSummaries,
} from './team-sprint-metrics-formatting'

const { Text } = Typography

const METRICS: { kind: SprintMetricKind; title: string }[] = [
  { kind: 'velocity', title: 'Average Velocity' },
  { kind: 'predictability', title: 'Average Predictability' },
  { kind: 'sayDo', title: 'Average Say/Do' },
]

export interface TeamSprintMetricsSummaryProps {
  teamId: string
  /** Opens the team's sprint metrics report, which lists the sprints behind the averages. */
  onOpenReport?: () => void
}

/**
 * A team's average velocity, predictability and say/do over the organization's
 * sprint window, in the team's sizing method. Every viewer sees the same
 * figures: there is no window or unit to choose here, unlike the report.
 *
 * Renders nothing for a team with no sprint to measure, so a page showing it
 * carries no empty section.
 */
const TeamSprintMetricsSummary: FC<TeamSprintMetricsSummaryProps> = ({
  teamId,
  onOpenReport,
}) => {
  const { data: metrics, isLoading } = useGetTeamSprintMetricsQuery(
    { teamId },
    { skip: !teamId },
  )

  if (!isLoading && (!metrics || metrics.sprints.length === 0)) return null

  const sprintCount = metrics?.sprints.length ?? 0
  // Fewer than the sprints counted when the team has changed its sizing method
  // since: those sprints' estimates are in another unit, and are not averaged.
  const inEstimate =
    metrics?.sprints.filter((s) => s.inEstimateAverages).length ?? 0
  // With no sprint yet in the team's sizing method there is nothing to average
  // in it, so the sprints are counted by work item instead.
  const showsCount =
    metrics?.sizingMethod === SizingMethod.Count || inEstimate === 0
  const summaries = metrics
    ? sprintMetricSummaries(metrics, showsCount)
    : undefined
  const teamMeasure = sizingMethodMeasure(
    metrics?.sizingMethod ?? SizingMethod.Count,
  )
  const measure = showsCount
    ? sizingMethodMeasure(SizingMethod.Count)
    : teamMeasure

  const covers = (() => {
    const last = `Last ${sprintCount} completed standard ${sprintCount === 1 ? 'sprint' : 'sprints'}`
    if (metrics?.sizingMethod === SizingMethod.Count) return last
    if (inEstimate === 0)
      return `${last}, by work item: none is sized in ${teamMeasure} yet`
    if (inEstimate < sprintCount)
      return `${last}: the ${sprintCountText(inEstimate)} sized in ${teamMeasure}`
    return last
  })()

  return (
    <Flex vertical gap="small">
      <Flex gap="small" align="baseline" wrap>
        <Text strong style={{ fontSize: 14 }}>
          Sprint Metrics
        </Text>
        {metrics && <Text type="secondary">{covers}</Text>}
      </Flex>
      <Flex gap="small" wrap>
        {METRICS.map(({ kind, title }) => (
          <MetricCard
            key={kind}
            title={title}
            value={formatSprintMetricAverage(kind, summaries?.[kind])}
            secondaryValue={formatSprintMetricRange(kind, summaries?.[kind])}
            tooltip={`${METRIC_TOOLTIPS[kind]} Measured in ${measure}${summaries?.[kind] ? `, over ${sprintCountText(summaries[kind].sprints)}` : ''}.`}
            loading={isLoading}
            cardStyle={METRIC_CARD_FLEX}
            onClick={onOpenReport}
            ariaLabel={`${title}: open the sprint metrics report`}
          />
        ))}
      </Flex>
    </Flex>
  )
}

export default TeamSprintMetricsSummary
