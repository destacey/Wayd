'use client'

import { CSSProperties } from 'react'
import { Alert, Flex, Typography } from 'antd'
import { MetricCard } from '@/src/components/common/metrics'
import {
  SprintCapacityDto,
  SprintCapacityOutcome,
  SprintCapacityScopeBasis,
} from '@/src/services/wayd-api'
import { useGetSprintCapacityQuery } from '@/src/store/features/work-management/sprints-api'
import { sizingMethodMeasure } from '@/src/utils'
import { formatPersonDays } from '@/src/app/organizations/_components/team-capacity/team-capacity-data'

const { Text } = Typography

const METRIC_GRID: CSSProperties = {
  display: 'grid',
  gridTemplateColumns: 'repeat(auto-fill, minmax(150px, 1fr))',
  gap: 8,
}

/** Why a sprint's scope was not set against its capacity, for a line under the figures. */
export const notComparedText: Partial<Record<SprintCapacityOutcome, string>> = {
  [SprintCapacityOutcome.NoCapacity]:
    "No one is allocated to the team for the sprint's dates in a role that counts toward delivery, so its scope isn't set against capacity.",
  [SprintCapacityOutcome.NoPriorSprints]:
    "The team has no completed sprints in this sizing method to learn from yet, so its scope isn't set against capacity.",
  [SprintCapacityOutcome.NoPriorCapacity]:
    "No one was allocated to the team during its recent sprints, so what a person-day delivers isn't known yet.",
}

/** "last sprint", or "last 3 sprints", with an optional word before "sprint". */
export const lastSprints = (count: number, kind = ''): string =>
  count === 1 ? `last ${kind}sprint` : `last ${count} ${kind}sprints`

/** The over capacity warning's text, or null when there is none. */
export function overCapacityWarning(
  capacity: SprintCapacityDto,
): { title: string; description: string } | null {
  if (!capacity.isOverCapacity || capacity.expectedScope == null) return null

  const measure = sizingMethodMeasure(capacity.sizingMethod)
  const scope =
    capacity.scopeBasis === SprintCapacityScopeBasis.Committed
      ? 'committed'
      : 'planned'
  return {
    title: `Over capacity: ${capacity.scope.toLocaleString()} ${measure} ${scope}, about ${Math.round(capacity.expectedScope).toLocaleString()} expected.`,
    description: `At what the team's ${lastSprints(capacity.priorSprints)} delivered per person-day of delivery capacity, ${formatPersonDays(capacity.deliveryPersonDays)} person-days is expected to deliver about ${Math.round(capacity.expectedScope).toLocaleString()} ${measure}.`,
  }
}

export interface SprintCapacitySummaryProps {
  sprintKey: number
}

/**
 * A sprint's capacity beside its scope: the delivery person-days its team has
 * for its planned dates, the work planned or committed, and what the capacity
 * is expected to deliver, with a warning when the scope is above it.
 */
const SprintCapacitySummary = ({ sprintKey }: SprintCapacitySummaryProps) => {
  // `currentData`, not `data`: `data` keeps the previous sprint's figures while
  // the next sprint's load, and they would be shown under it.
  const { currentData: capacity, isFetching } =
    useGetSprintCapacityQuery(sprintKey)

  if (isFetching && !capacity) {
    return (
      <div style={METRIC_GRID}>
        <MetricCard title="Capacity" value={0} loading />
      </div>
    )
  }
  if (!capacity) return null

  const measure = sizingMethodMeasure(capacity.sizingMethod)
  const committed = capacity.scopeBasis === SprintCapacityScopeBasis.Committed
  const warning = overCapacityWarning(capacity)
  const reason = notComparedText[capacity.outcome]

  return (
    <Flex vertical gap={8}>
      {warning && (
        <Alert
          type="warning"
          showIcon
          title={warning.title}
          description={warning.description}
        />
      )}
      <div style={METRIC_GRID}>
        <MetricCard
          title="Capacity"
          value={formatPersonDays(capacity.deliveryPersonDays)}
          // The unit sits beneath: as a suffix it runs past a narrow card.
          secondaryValue={
            capacity.awayPersonDays > 0
              ? `person-days · ${formatPersonDays(capacity.awayPersonDays)} away`
              : 'person-days'
          }
          tooltip="The team's delivery capacity over the sprint's planned dates: the time allocated to it in roles that count toward delivery, after each person's holidays and time away."
        />
        <MetricCard
          title={committed ? 'Committed' : 'Planned'}
          value={capacity.scope}
          tooltip={
            committed
              ? `The ${measure} in the sprint at its commitment point, as estimated then.`
              : `The ${measure} in the sprint now. Once the sprint passes its commitment point, the committed ${measure} are set against its capacity instead.`
          }
        />
        {capacity.expectedScope != null && (
          <MetricCard
            title="Expected"
            value={Math.round(capacity.expectedScope)}
            tooltip={`What the capacity is expected to deliver: the ${measure} the team completed per person-day of delivery capacity over its ${lastSprints(capacity.priorSprints, 'completed ')}, times this sprint's person-days.`}
          />
        )}
      </div>
      {reason && <Text type="secondary">{reason}</Text>}
    </Flex>
  )
}

export default SprintCapacitySummary
