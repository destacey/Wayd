'use client'

import {
  CycleTimeMetric,
  DaysCountdownMetric,
  HealthMetric,
  MetricCard,
  StatusMetric,
  sprintOverviewFigures,
} from '@/src/components/common/metrics'
import { IterationHealthIndicator } from '@/src/components/common/planning'
import useTheme from '@/src/components/contexts/theme'
import { IterationState } from '@/src/components/types'
import { SizingMethod, SprintDetailsDto } from '@/src/services/wayd-api'
import {
  useGetSprintMetricsQuery,
  useGetSprintScopeQuery,
} from '@/src/store/features/work-management/sprints-api'
import { Alert, Flex, Segmented, Skeleton, Typography } from 'antd'
import { WaydTooltip } from '@/src/components/common'
import {
  sizingMethodLabel,
  sizingMethodMeasure,
  sprintActiveDays,
} from '@/src/utils'
import { CSSProperties, FC, ReactNode, useEffect, useState } from 'react'
import { sprintScopeWindowText } from './sprint-scope-window-text'

const { Text } = Typography

const COUNT = 'Count'

const METRIC_GRID: CSSProperties = {
  display: 'grid',
  gridTemplateColumns: 'repeat(auto-fill, minmax(150px, 1fr))',
  gap: 8,
}

export interface SprintMetricsProps {
  sprint: SprintDetailsDto
  onHealthIndicatorReady?: (indicator: ReactNode) => void
}

/**
 * A sprint's overview figures: what it committed to and what became of it,
 * from its scope, beside the work in it now. One switch measures them all in
 * the sprint's estimate or by item count.
 */
const SprintMetrics: FC<SprintMetricsProps> = ({
  sprint,
  onHealthIndicatorReady,
}) => {
  const [byCount, setByCount] = useState(false)
  const { token } = useTheme()

  const { data: metrics, isLoading: metricsLoading } = useGetSprintMetricsQuery(
    sprint.key,
  )
  const { data: scope, isLoading: scopeLoading } = useGetSprintScopeQuery(
    sprint.key,
  )
  const isLoading = metricsLoading || scopeLoading
  const activeDays = sprintActiveDays(sprint)
  const isActive = sprint.state.id === IterationState.Active

  // The sprint is measured in its team's sizing method on its planned start, which the metrics report.
  const sizingMethod = metrics?.sizingMethod ?? SizingMethod.Count
  const isCountSized = sizingMethod === SizingMethod.Count
  const showsCount = byCount || isCountSized
  const measure = sizingMethodMeasure(
    showsCount ? SizingMethod.Count : sizingMethod,
  )

  const figures = sprintOverviewFigures(metrics, scope, showsCount)
  const scopeFigures = figures.scope
  const showsUnestimated = !showsCount && !!metrics

  useEffect(() => {
    if (!isLoading && metrics && onHealthIndicatorReady) {
      onHealthIndicatorReady(
        <IterationHealthIndicator
          startDate={activeDays.start}
          endDate={activeDays.end}
          total={figures.completionBase}
          completed={figures.completed}
        />,
      )
    }
  }, [
    activeDays.end,
    activeDays.start,
    figures.completed,
    figures.completionBase,
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
      {scope?.historyIncomplete && (
        <Alert
          type="warning"
          showIcon
          title="History incomplete — run a full sync"
          description="Committed, added, carried over and descoped work and say/do come from work item history, which has not yet been read in full for every workspace holding this sprint's work. A full sync of the connection completes it; until then, completion is measured on the items in the sprint now."
        />
      )}
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
        Both rows share one column template, so a card in the second row sits
        under one in the first. auto-fill keeps empty tracks, which is what
        holds the shorter commitment row to the progress row's columns; each
        column is at least wide enough for "Avg Cycle Time" and "Days Remaining".

        The first row is progress — how the sprint's work is going; the second
        is commitment — what the sprint committed to and what became of it.
      */}
      <div style={METRIC_GRID}>
        {isActive && (
          <DaysCountdownMetric
            state={sprint.state.id as IterationState}
            startDate={activeDays.start}
            endDate={activeDays.end}
          />
        )}
        {scopeFigures?.predictability != null && (
          <MetricCard
            title="Predictability"
            value={scopeFigures.predictability * 100}
            precision={0}
            suffix="%"
            tooltip={`Velocity ÷ the ${measure} committed at the commitment point, up to 100%. Finished work added after the commitment point counts toward it.`}
          />
        )}
        <MetricCard
          title="Velocity"
          value={figures.completed}
          valueStyle={{ color: token.colorSuccess }}
          secondaryValue={
            scopeFigures && scopeFigures.removed > 0
              ? `${scopeFigures.removed.toLocaleString()} as Removed`
              : undefined
          }
          tooltip={
            scopeFigures
              ? `The ${measure} completed while in the sprint, including work completed and then moved out. Unlike most tools' velocity, work moved to a Removed status in the sprint counts too, shown beneath.`
              : `The ${measure} in the sprint that are done (Status Category: Done or Removed).`
          }
        />
        {isActive && (
          <>
            <StatusMetric
              title="In Progress"
              value={figures.inProgress}
              total={figures.currentTotal}
              color={token.colorInfo}
              tooltip={`The ${measure} in the sprint now that are in progress (Status Category: Active). The percentage is their share of the sprint's work now.`}
            />
            <StatusMetric
              title="Not Started"
              value={figures.notStarted}
              total={figures.currentTotal}
              tooltip={`The ${measure} in the sprint now that are not started (Status Category: Proposed). The percentage is their share of the sprint's work now.`}
            />
          </>
        )}
        {metrics?.cycleTime && metrics.cycleTime.workItemsCount > 0 && (
          <CycleTimeMetric
            value={metrics.cycleTime.averageCycleTimeDays ?? 0}
            tooltip="The average cycle time of done work items in the sprint (in days). Cycle time measures the time from when work starts (Activated) to when it's completed (Done)."
          />
        )}
      </div>
      {(scopeFigures || showsUnestimated) && (
        <div style={METRIC_GRID}>
          {scopeFigures && (
            <>
              <MetricCard
                title="Committed"
                value={scopeFigures.committed}
                tooltip={`The ${measure} in the sprint at its commitment point, as estimated then.`}
              />
              <MetricCard
                title="Added"
                value={scopeFigures.added}
                tooltip={`The ${measure} that entered the sprint after its commitment point, as estimated when added.`}
              />
              {(!isActive || scopeFigures.carriedOver > 0) && (
                <MetricCard
                  title="Carried Over"
                  value={scopeFigures.carriedOver}
                  tooltip={`Unfinished ${measure} still in the sprint at its end, or moved to the team's next sprint on its last day.`}
                />
              )}
              <MetricCard
                title="Descoped"
                value={scopeFigures.descoped}
                tooltip={`Unfinished ${measure} taken out of the sprint before its last day, or on it for somewhere other than the team's next sprint.`}
              />
              {scopeFigures.sayDo !== null && (
                <MetricCard
                  title={isActive ? 'Say/Do so far' : 'Say/Do'}
                  value={scopeFigures.sayDo * 100}
                  precision={0}
                  suffix="%"
                  tooltip="Of the work in the sprint at its commitment point, the share completed by its end. Work added later does not count, and an item completed as Removed counts as completed."
                />
              )}
            </>
          )}
          {showsUnestimated && (
            <HealthMetric
              title="Unestimated"
              value={metrics.unestimatedWorkItems}
              severity="warning"
              tooltip={`Number of work items in the sprint with no ${sizingMethodMeasure(sizingMethod)}. An estimate of 0 counts as estimated.`}
            />
          )}
        </div>
      )}
      {scopeFigures && scope && (
        <Flex vertical>
          {sprintScopeWindowText(scope).map((line) => (
            <Text key={line} type="secondary">
              {line}
            </Text>
          ))}
        </Flex>
      )}
    </Flex>
  )
}

export default SprintMetrics
