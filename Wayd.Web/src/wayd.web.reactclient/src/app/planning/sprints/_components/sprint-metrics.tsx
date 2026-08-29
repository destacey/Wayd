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
} from '@/src/components/common/metrics'
import { IterationHealthIndicator } from '@/src/components/common/planning'
import useTheme from '@/src/components/contexts/theme'
import { IterationState } from '@/src/components/types'
import { SizingMethod, SprintDetailsDto } from '@/src/services/wayd-api'
import { useGetSprintMetricsQuery } from '@/src/store/features/planning/sprints-api'
import { Flex, Segmented, Skeleton } from 'antd'
import { WaydTooltip } from '@/src/components/common'
import { FC, ReactNode, useEffect, useState } from 'react'

export interface SprintMetricsProps {
  sprint: SprintDetailsDto
  sizingMethod?: SizingMethod
  onHealthIndicatorReady?: (indicator: ReactNode) => void
}

const SprintMetrics: FC<SprintMetricsProps> = ({
  sprint,
  sizingMethod = SizingMethod.Count,
  onHealthIndicatorReady,
}) => {
  const [sizingMethodState, setSizingMethodState] =
    useState<SizingMethod>(sizingMethod)
  const { token } = useTheme()

  const useStoryPoints = sizingMethodState === SizingMethod.StoryPoints

  const { data: metrics, isLoading } = useGetSprintMetricsQuery(sprint.key)

  // Update local state when sizingMethod prop changes
  // This allows the component to be both controlled (responds to prop changes)
  // and uncontrolled (maintains local state for user interactions)
  useEffect(() => {
    if (sizingMethod) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setSizingMethodState(sizingMethod)
    }
  }, [sizingMethod])

  const displayValues = !metrics
    ? {
        total: 0,
        completed: 0,
        inProgress: 0,
        notStarted: 0,
      }
    : {
        total: useStoryPoints
          ? metrics.totalStoryPoints
          : metrics.totalWorkItems,
        completed: useStoryPoints
          ? metrics.completedStoryPoints
          : metrics.completedWorkItems,
        inProgress: useStoryPoints
          ? metrics.inProgressStoryPoints
          : metrics.inProgressWorkItems,
        notStarted: useStoryPoints
          ? metrics.notStartedStoryPoints
          : metrics.notStartedWorkItems,
      }

  // Notify parent when health indicator is ready
  useEffect(() => {
    if (!isLoading && metrics && onHealthIndicatorReady) {
      onHealthIndicatorReady(
        <IterationHealthIndicator
          startDate={new Date(sprint.start)}
          endDate={new Date(sprint.end)}
          total={displayValues.total}
          completed={displayValues.completed}
        />,
      )
    }
  }, [
    displayValues.completed,
    displayValues.total,
    isLoading,
    metrics,
    onHealthIndicatorReady,
    sprint.end,
    sprint.start,
  ])

  if (isLoading) {
    return <Skeleton active />
  }

  return (
    <Flex vertical gap="small">
      <Flex gap="small" justify="flex-end">
        <WaydTooltip title="Switch between counting work items and summing story points for metrics">
          <Segmented<string>
            options={['Count', 'Story Points']}
            value={useStoryPoints ? 'Story Points' : 'Count'}
            onChange={(value) =>
              setSizingMethodState(
                value === 'Story Points'
                  ? SizingMethod.StoryPoints
                  : SizingMethod.Count,
              )
            }
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
            startDate={sprint.start}
            endDate={sprint.end}
            cardStyle={METRIC_CARD_FLEX}
          />
        )}
        <CompletionRateMetric
          completed={displayValues.completed}
          total={displayValues.total}
          tooltip={sizingMethodState}
          cardStyle={METRIC_CARD_FLEX}
        />
        <MetricCard
          title="Total"
          value={displayValues.total}
          tooltip="Total number of story points or items currently in the sprint."
          cardStyle={METRIC_CARD_FLEX}
        />
        <VelocityMetric
          completed={displayValues.completed}
          total={displayValues.total}
          tooltip={sizingMethodState}
          cardStyle={METRIC_CARD_FLEX}
        />
        <StatusMetric
          title="In Progress"
          value={displayValues.inProgress}
          total={displayValues.total}
          color={token.colorInfo}
          tooltip="Total number of story points or items currently in the sprint that are in progress (Status Category: Active). Percentage shown represents the portion of total sprint work that is in progress."
          cardStyle={METRIC_CARD_FLEX}
        />
        <StatusMetric
          title="Not Started"
          value={displayValues.notStarted}
          total={displayValues.total}
          tooltip="Total number of story points or items currently in the sprint that are not started (Status Category: Proposed). Percentage shown represents the portion of total sprint work that has not been started."
          cardStyle={METRIC_CARD_FLEX}
        />
        {sprint.state.id === IterationState.Active && metrics && (
          <StatusMetric
            title="WIP"
            value={metrics.inProgressWorkItems}
            total={displayValues.total}
            tooltip="Work In Progress - Count of active work items (Status Category: Active). Percentage shown represents the portion of total sprint work that is currently in progress."
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
        {useStoryPoints && metrics && (
          <HealthMetric
            title="Missing SPs"
            value={metrics.missingStoryPointsCount}
            tooltip="Number of work items in the sprint that don't have story points assigned."
            cardStyle={METRIC_CARD_FLEX}
          />
        )}
      </Flex>
    </Flex>
  )
}

export default SprintMetrics
