'use client'

import { Flex, Typography } from 'antd'
import { FC } from 'react'
import { SizingMethod } from '@/src/services/wayd-api'
import { useGetTeamSprintMetricsQuery } from '@/src/store/features/organizations/team-api'
import { sizingMethodMeasure } from '@/src/utils'
import { METRIC_CARD_FLEX, MetricCard } from '../../metrics'
import {
  formatSprintMetric,
  formatSprintMetricRange,
  METRIC_TOOLTIPS,
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

  const isCountSized = metrics?.sizingMethod === SizingMethod.Count
  const summaries = metrics
    ? sprintMetricSummaries(metrics, isCountSized)
    : undefined
  const measure = sizingMethodMeasure(
    metrics?.sizingMethod ?? SizingMethod.Count,
  )
  const sprintCount = metrics?.sprints.length ?? 0

  return (
    <Flex vertical gap="small">
      <Flex gap="small" align="baseline" wrap>
        <Text strong style={{ fontSize: 14 }}>
          Sprint Metrics
        </Text>
        {metrics && (
          <Text type="secondary">
            {`Last ${sprintCount} completed standard ${sprintCount === 1 ? 'sprint' : 'sprints'}`}
          </Text>
        )}
      </Flex>
      <Flex gap="small" wrap>
        {METRICS.map(({ kind, title }) => (
          <MetricCard
            key={kind}
            title={title}
            value={formatSprintMetric(kind, summaries?.[kind]?.average)}
            secondaryValue={formatSprintMetricRange(kind, summaries?.[kind])}
            tooltip={`${METRIC_TOOLTIPS[kind]} Measured in ${measure}.`}
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
