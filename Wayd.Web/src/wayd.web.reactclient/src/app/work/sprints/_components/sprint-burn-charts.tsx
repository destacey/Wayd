'use client'

import { SizingMethod } from '@/src/services/wayd-api'
import { useGetSprintBurnQuery } from '@/src/store/features/work-management/sprints-api'
import { sizingMethodMeasure } from '@/src/utils'
import { theme } from 'antd'
import { CSSProperties, FC } from 'react'
import BurnChart from '@/src/components/common/planning/burn-chart'
import {
  BurnSeries,
  sprintBurnSeries,
} from '@/src/components/common/planning/sprint-burn-series'

// Two charts side by side when there is room, stacked when there is not.
const CHARTS_GRID: CSSProperties = {
  display: 'grid',
  gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))',
  gap: 8,
}

export interface SprintBurnChartsProps {
  sprintKey: number
  /** Counts items rather than summing the sprint's estimate; the overview's switch. */
  byCount: boolean
  sizingMethod: SizingMethod
}

/**
 * A sprint's burn-up and burn-down, from work item history. Nothing while the
 * sprint's history is incomplete — the overview already says so — or when the
 * burn could not be read.
 */
const SprintBurnCharts: FC<SprintBurnChartsProps> = ({
  sprintKey,
  byCount,
  sizingMethod,
}) => {
  const { token } = theme.useToken()
  const { data: burn, isLoading, isError } = useGetSprintBurnQuery(sprintKey)

  // A failed read shows nothing rather than an empty chart that would read as
  // a sprint not yet started.
  if (isError || burn?.historyIncomplete) return null

  const { burnUp, burnDown } = sprintBurnSeries(burn, byCount)
  const measure = sizingMethodMeasure(
    byCount ? SizingMethod.Count : sizingMethod,
  )
  const timeZone = burn?.timeZone ?? 'UTC'
  const start = new Date(burn?.effectiveStart ?? 0)
  const end = new Date(burn?.effectiveEnd ?? 0)

  return (
    <div style={CHARTS_GRID}>
      <BurnChart
        title="Burn-up"
        tooltip={`The ${measure} in the sprint and the part of it completed, at the end of each day in the team's zone. Work added after the commitment point steps scope up; descoped work steps it down.`}
        data={burnUp}
        series={[BurnSeries.Scope, BurnSeries.Completed]}
        colors={[token.colorTextTertiary, token.colorSuccess]}
        timeZone={timeZone}
        measure={measure}
        start={start}
        end={end}
        isLoading={isLoading}
      />
      <BurnChart
        title="Burn-down"
        tooltip={`The ${measure} still to do at the end of each day in the team's zone. Reopened work counts as remaining again. The ideal line falls evenly from the committed ${measure} to zero at the sprint's end.`}
        data={burnDown}
        series={[BurnSeries.Remaining, BurnSeries.Ideal]}
        colors={[token.colorPrimary, token.colorTextQuaternary]}
        timeZone={timeZone}
        measure={measure}
        start={start}
        end={end}
        isLoading={isLoading}
      />
    </div>
  )
}

export default SprintBurnCharts
