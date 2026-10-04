'use client'

import {
  CompletionRateMetric,
  CycleTimeMetric,
  DaysCountdownMetric,
  HealthMetric,
  METRIC_CARD_FLEX,
  MetricCard,
  StatusMetric,
  VelocityMetric,
  sprintMetricValues,
} from '@/src/components/common/metrics'
import { IterationHealthIndicator } from '@/src/components/common/planning'
import useTheme from '@/src/components/contexts/theme'
import { IterationState } from '@/src/components/types'
import { SizingMethod, SprintDetailsDto } from '@/src/services/wayd-api'
import { useGetSprintMetricsQuery } from '@/src/store/features/work-management/sprints-api'
import { Flex, Segmented, Skeleton } from 'antd'
import { WaydTooltip } from '@/src/components/common'
import {
  sizingMethodLabel,
  sizingMethodMeasure,
  sprintActiveDays,
} from '@/src/utils'
import { FC, ReactNode, useEffect, useState } from 'react'

const COUNT = 'Count'

export interface SprintMetricsProps {
  sprint: SprintDetailsDto
  onHealthIndicatorReady?: (indicator: ReactNode) => void
}

const SprintMetrics: FC<SprintMetricsProps> = ({
  sprint,
  onHealthIndicatorReady,
}) => {
  const [byCount, setByCount] = useState(false)
  const { token } = useTheme()

  const { data: metrics, isLoading } = useGetSprintMetricsQuery(sprint.key)
  const activeDays = sprintActiveDays(sprint)

  // The sprint is measured in its team's sizing method on its planned start, which the metrics report.
  const sizingMethod = metrics?.sizingMethod ?? SizingMethod.Count
  const isCountSized = sizingMethod === SizingMethod.Count
  const showsCount = byCount || isCountSized
  const measure = sizingMethodMeasure(
    showsCount ? SizingMethod.Count : sizingMethod,
  )

  const displayValues = sprintMetricValues(metrics, showsCount)

  // Notify parent when health indicator is ready
  useEffect(() => {
    if (!isLoading && metrics && onHealthIndicatorReady) {
      onHealthIndicatorReady(
        <IterationHealthIndicator
          startDate={activeDays.start}
          endDate={activeDays.end}
          total={displayValues.total}
          completed={displayValues.completed}
        />,
      )
    }
  }, [
    activeDays.end,
    activeDays.start,
    displayValues.completed,
    displayValues.total,
    isLoading,
    metrics,
    onHealthIndicatorReady,
  ])

  if (isLoading) {
    return <Skeleton active />
  }

  const unitLabel = sizingMethodLabel(sizingMethod)

  return (
    <Flex vertical gap="small">
      <Flex gap="small" justify="flex-end">
        <WaydTooltip
          title={
            isCountSized
              ? "This sprint's team sizes by count, so its metrics count work items."
              : `Switch between summing ${sizingMethodMeasure(sizingMethod)}, the team's sizing method for this sprint, and counting work items`
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
      {/*
        A wrapping flex row rather than Row/Col: the 24-column grid splits the
        available width into fixed fractions whichever way the labels fall, so
        in a record page's narrower content column the same span clipped
        "Avg Cycle Time" and "Days Remaining". Here each card states the width
        it needs and the row wraps when they no longer fit.
      */}
      <Flex wrap gap={8}>
        {sprint.state.id !== IterationState.Completed && (
          <DaysCountdownMetric
            state={sprint.state.id as IterationState}
            startDate={activeDays.start}
            endDate={activeDays.end}
            cardStyle={METRIC_CARD_FLEX}
          />
        )}
        <CompletionRateMetric
          completed={displayValues.completed}
          total={displayValues.total}
          tooltip={showsCount ? SizingMethod.Count : sizingMethod}
          cardStyle={METRIC_CARD_FLEX}
        />
        <MetricCard
          title="Total"
          value={displayValues.total}
          tooltip={`Total ${measure} currently in the sprint.`}
          cardStyle={METRIC_CARD_FLEX}
        />
        <VelocityMetric
          completed={displayValues.completed}
          total={displayValues.total}
          tooltip={showsCount ? SizingMethod.Count : sizingMethod}
          cardStyle={METRIC_CARD_FLEX}
        />
        <StatusMetric
          title="In Progress"
          value={displayValues.inProgress}
          total={displayValues.total}
          color={token.colorInfo}
          tooltip={`Total ${measure} currently in the sprint that are in progress (Status Category: Active). Percentage shown represents the portion of total sprint work that is in progress.`}
          cardStyle={METRIC_CARD_FLEX}
        />
        <StatusMetric
          title="Not Started"
          value={displayValues.notStarted}
          total={displayValues.total}
          tooltip={`Total ${measure} currently in the sprint that are not started (Status Category: Proposed). Percentage shown represents the portion of total sprint work that has not been started.`}
          cardStyle={METRIC_CARD_FLEX}
        />
        {sprint.state.id === IterationState.Active && metrics && (
          <StatusMetric
            title="WIP"
            value={metrics.inProgressWorkItems}
            total={metrics.totalWorkItems}
            tooltip="Work In Progress - Count of active work items (Status Category: Active). Percentage shown represents the portion of the sprint's work items that are currently in progress."
            cardStyle={METRIC_CARD_FLEX}
          />
        )}
        {metrics?.cycleTime && metrics.cycleTime.workItemsCount > 0 && (
          <CycleTimeMetric
            value={metrics.cycleTime.averageCycleTimeDays ?? 0}
            tooltip="The average cycle time of done work items in the sprint (in days). Cycle time measures the time from when work starts (Activated) to when it's completed (Done)."
            cardStyle={METRIC_CARD_FLEX}
          />
        )}
        {!showsCount && metrics && (
          <HealthMetric
            title="Unestimated"
            value={metrics.unestimatedWorkItems}
            tooltip={`Number of work items in the sprint with no ${sizingMethodMeasure(sizingMethod)}. An estimate of 0 counts as estimated.`}
            cardStyle={METRIC_CARD_FLEX}
          />
        )}
      </Flex>
    </Flex>
  )
}

export default SprintMetrics
