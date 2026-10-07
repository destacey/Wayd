'use client'

import { ChartCard } from '@/src/components/common/metrics'
import WaydEmpty from '@/src/components/common/wayd-empty'
import useTheme from '@/src/components/contexts/theme'
import { useChartRemountOnResize } from '@/src/hooks'
import { SizingMethod } from '@/src/services/wayd-api'
import { useGetSprintBurnQuery } from '@/src/store/features/work-management/sprints-api'
import { sizingMethodMeasure } from '@/src/utils'
import { theme } from 'antd'
import dynamic from 'next/dynamic'
import { CSSProperties, FC } from 'react'
import {
  BurnChartPoint,
  BurnSeries,
  sprintBurnSeries,
} from './sprint-burn-series'

const Line = dynamic(
  () => import('@ant-design/charts').then((mod) => mod.Line) as any,
  { ssr: false },
)

const CHART_HEIGHT = 260

// Two charts side by side when there is room, stacked when there is not.
const CHARTS_GRID: CSSProperties = {
  display: 'grid',
  gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))',
  gap: 8,
}

interface BurnChartProps {
  title: string
  tooltip: string
  data: BurnChartPoint[]
  series: BurnSeries[]
  colors: string[]
  timeZone: string
  measure: string
  isLoading: boolean
}

const BurnChart: FC<BurnChartProps> = ({
  title,
  tooltip,
  data,
  series,
  colors,
  timeZone,
  measure,
  isLoading,
}) => {
  const { antDesignChartsTheme } = useTheme()
  const { ref, renderKey } = useChartRemountOnResize()

  // The sprint's days are the team's, so its axis and readings are too.
  const day = new Intl.DateTimeFormat('en-US', {
    timeZone,
    month: 'short',
    day: 'numeric',
  })
  const moment = new Intl.DateTimeFormat('en-US', {
    timeZone,
    month: 'short',
    day: 'numeric',
    hour: 'numeric',
    minute: '2-digit',
    timeZoneName: 'short',
  })

  const config = {
    theme: antDesignChartsTheme,
    autoFit: true,
    height: CHART_HEIGHT,
    data,
    xField: 'at',
    yField: 'value',
    colorField: 'series',
    scale: {
      x: { type: 'time' },
      y: { nice: true, domainMin: 0 },
      color: { domain: series, range: colors },
    },
    style: {
      lineWidth: 2,
      // The ideal line is a reference, not a reading.
      lineDash: (points: BurnChartPoint[]) =>
        points[0]?.series === BurnSeries.Ideal ? [6, 4] : [0, 0],
    },
    axis: {
      x: {
        labelFormatter: (value: Date) => day.format(value),
        gridStrokeOpacity: 0.2,
      },
      y: { title: measure, gridStrokeOpacity: 0.2 },
    },
    legend: {
      color: { layout: { justifyContent: 'center' }, itemMarker: 'line' },
    },
    interaction: { tooltip: { crosshairs: true, shared: true } },
    tooltip: {
      title: (datum: BurnChartPoint) => moment.format(datum.at),
      items: [
        {
          channel: 'y',
          valueFormatter: (value: number) =>
            Number.isInteger(value) ? value.toLocaleString() : value.toFixed(1),
        },
      ],
    },
  }

  return (
    <ChartCard
      title={title}
      tooltip={tooltip}
      loading={isLoading}
      skeletonHeight={CHART_HEIGHT}
    >
      <div ref={ref}>
        {data.length === 0 ? (
          <WaydEmpty message="No readings yet: the sprint has not reached its commitment point." />
        ) : (
          <Line key={renderKey} {...config} />
        )}
      </div>
    </ChartCard>
  )
}

export interface SprintBurnChartsProps {
  sprintKey: number
  /** Counts items rather than summing the sprint's estimate; the overview's switch. */
  byCount: boolean
  sizingMethod: SizingMethod
}

/**
 * A sprint's burn-up and burn-down, from work item history. Nothing while the
 * sprint's history is incomplete: the overview already says so.
 */
const SprintBurnCharts: FC<SprintBurnChartsProps> = ({
  sprintKey,
  byCount,
  sizingMethod,
}) => {
  const { token } = theme.useToken()
  const { data: burn, isLoading } = useGetSprintBurnQuery(sprintKey)

  if (burn?.historyIncomplete) return null

  const { burnUp, burnDown } = sprintBurnSeries(burn, byCount)
  const measure = sizingMethodMeasure(
    byCount ? SizingMethod.Count : sizingMethod,
  )
  const timeZone = burn?.timeZone ?? 'UTC'

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
        isLoading={isLoading}
      />
    </div>
  )
}

export default SprintBurnCharts
