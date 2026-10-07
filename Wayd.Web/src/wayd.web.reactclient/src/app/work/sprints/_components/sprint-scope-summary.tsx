'use client'

import { WaydTooltip } from '@/src/components/common'
import { METRIC_CARD_FLEX, MetricCard } from '@/src/components/common/metrics'
import SprintSayDoMetric from '@/src/components/common/planning/sprint-say-do-metric'
import {
  SizingMethod,
  SprintDetailsDto,
  SprintScopeDto,
  SprintScopeMeasureDto,
} from '@/src/services/wayd-api'
import { useGetSprintScopeQuery } from '@/src/store/features/work-management/sprints-api'
import {
  formatInstantInZone,
  sizingMethodLabel,
  sizingMethodMeasure,
} from '@/src/utils'
import { Alert, Flex, Segmented, Skeleton, Typography } from 'antd'
import { FC, useState } from 'react'

const { Text } = Typography

const COUNT = 'Count'

export interface SprintScopeSummaryProps {
  sprint: SprintDetailsDto
}

/**
 * Where the scope's commitment point and end came from, and the zone their
 * times are in.
 */
export const sprintScopeWindowText = (scope: SprintScopeDto): string[] => {
  const start = formatInstantInZone(scope.effectiveStart, scope.timeZone)
  const end = formatInstantInZone(scope.effectiveEnd, scope.timeZone)

  return [
    `Committed at ${start}, ${
      scope.startIsActual
        ? 'when the team started the sprint'
        : 'the end of the commitment grace period after the planned start'
    }.`,
    `Ends at ${end}, ${
      scope.endIsActual
        ? 'when the team completed the sprint'
        : 'the end of the last planned day, or the next sprint’s start if sooner'
    }.`,
    scope.hasTeam
      ? `Times are in the team’s zone, ${scope.timeZone}.`
      : `This sprint has no team, so it uses the system default zone (${scope.timeZone}) and grace period, and counts work items.`,
  ]
}

/**
 * What the sprint committed to and what became of it, from work item history.
 */
const SprintScopeSummary: FC<SprintScopeSummaryProps> = ({ sprint }) => {
  const [byCount, setByCount] = useState(false)
  const { data: scope, isLoading } = useGetSprintScopeQuery(sprint.key)

  if (isLoading) return <Skeleton active />
  if (!scope) return null

  if (scope.historyIncomplete) {
    return (
      <Alert
        type="warning"
        showIcon
        title="History incomplete — run a full sync"
        description="Committed, added, carried over and descoped work come from work item history, which has not yet been read in full for every workspace holding this sprint's work. A full sync of the connection completes it."
      />
    )
  }

  const isCountSized = scope.sizingMethod === SizingMethod.Count
  const showsCount = byCount || isCountSized
  const unitLabel = sizingMethodLabel(scope.sizingMethod)
  const measure = sizingMethodMeasure(
    showsCount ? SizingMethod.Count : scope.sizingMethod,
  )
  const valueOf = (m: SprintScopeMeasureDto) =>
    showsCount ? m.count : m.estimate

  const { totals } = scope

  return (
    <Flex vertical gap="small">
      <Flex gap="small" justify="space-between" align="flex-start" wrap>
        <Flex vertical>
          <Text strong>Scope</Text>
          {sprintScopeWindowText(scope).map((line) => (
            <Text key={line} type="secondary">
              {line}
            </Text>
          ))}
        </Flex>
        <WaydTooltip
          title={
            isCountSized
              ? 'This sprint is sized by count, so its scope counts work items.'
              : `Switch between summing ${sizingMethodMeasure(scope.sizingMethod)} and counting work items`
          }
        >
          <Segmented<string>
            options={isCountSized ? [COUNT] : [unitLabel, COUNT]}
            value={showsCount ? COUNT : unitLabel}
            disabled={isCountSized}
            onChange={(value) => setByCount(value === COUNT)}
          />
        </WaydTooltip>
      </Flex>
      <Flex wrap gap={8}>
        <MetricCard
          title="Committed"
          value={valueOf(totals.committed)}
          tooltip={`The ${measure} in the sprint at its commitment point, as estimated then.`}
          cardStyle={METRIC_CARD_FLEX}
        />
        <MetricCard
          title="Added"
          value={valueOf(totals.added)}
          tooltip={`The ${measure} that entered the sprint after its commitment point, as estimated when added.`}
          cardStyle={METRIC_CARD_FLEX}
        />
        <MetricCard
          title="Completed"
          value={valueOf(totals.completed)}
          secondaryValue={
            totals.removed.count > 0
              ? `${valueOf(totals.removed).toLocaleString()} as Removed`
              : undefined
          }
          tooltip={`The ${measure} done while in the sprint. Work moved to a Removed status in the sprint counts as completed.`}
          cardStyle={METRIC_CARD_FLEX}
        />
        {totals.remaining.count > 0 && (
          <MetricCard
            title="Remaining"
            value={valueOf(totals.remaining)}
            tooltip={`Unfinished ${measure} still in the sprint, which has not ended.`}
            cardStyle={METRIC_CARD_FLEX}
          />
        )}
        <MetricCard
          title="Carried Over"
          value={valueOf(totals.carriedOver)}
          tooltip={`Unfinished ${measure} still in the sprint at its end, or moved to the team's next sprint on its last day.`}
          cardStyle={METRIC_CARD_FLEX}
        />
        <MetricCard
          title="Descoped"
          value={valueOf(totals.descoped)}
          tooltip={`Unfinished ${measure} taken out of the sprint before its last day, or on it for somewhere other than the team's next sprint.`}
          cardStyle={METRIC_CARD_FLEX}
        />
        <SprintSayDoMetric
          scope={scope}
          byCount={showsCount}
          cardStyle={METRIC_CARD_FLEX}
        />
      </Flex>
    </Flex>
  )
}

export default SprintScopeSummary
