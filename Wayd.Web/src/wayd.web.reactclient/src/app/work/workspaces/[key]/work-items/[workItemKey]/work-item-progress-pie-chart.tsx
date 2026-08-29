'use client'

import dynamic from 'next/dynamic'
import { WorkItemProgressDailyRollupDto } from '@/src/services/wayd-api'
import useTheme from '@/src/components/contexts/theme'
import {
  BREAKDOWN_CHART_HEIGHT,
  BREAKDOWN_LEGEND_HEIGHT,
} from '@/src/app/ppm/_components/breakdown-pie'
import { ChartCard } from '@/src/components/common/metrics'
import { getWorkStatusCategoryColorScale } from '@/src/components/common/work'
import { useChartRemountOnResize } from '@/src/hooks'
import { theme } from 'antd'

const Pie = dynamic(
  () => import('@ant-design/charts').then((mod) => mod.Pie) as any,
  { ssr: false },
)

export interface WorkItemProgressPieChartProps {
  progress?: WorkItemProgressDailyRollupDto | null
  isLoading: boolean
}

export const PROGRESS_TOOLTIP =
  'Every requirement-tier work item beneath this one, however deep — not just its direct children.'

const WorkItemProgressPieChart = ({
  progress,
  isLoading,
}: WorkItemProgressPieChartProps) => {
  const { antDesignChartsTheme } = useTheme()
  const { token } = theme.useToken()
  // Before the early return: hooks cannot be conditional.
  const { ref, renderKey } = useChartRemountOnResize()

  // Nothing to plot once loaded. While loading there is nothing to plot either,
  // but the card still renders so its skeleton is what the reader sees.
  if (
    !isLoading &&
    (!progress ||
      (progress.proposed === 0 && progress.active === 0 && progress.done === 0))
  )
    return null

  // Empty categories are dropped rather than passed as zeroes: a zero slice
  // draws no arc but still renders its label, stacking "0 (0%)" over the real
  // one at the centre of the circle. The scale's fixed domain keeps them in
  // the legend regardless.
  const data = [
    { type: 'Proposed', count: progress?.proposed ?? 0 },
    { type: 'Active', count: progress?.active ?? 0 },
    { type: 'Done', count: progress?.done ?? 0 },
  ].filter((d) => d.count > 0)

  const config = {
    // No `title`: `ChartCard` heads the chart, and G2 would draw a second one.
    theme: antDesignChartsTheme,
    data: data,
    angleField: 'count',
    colorField: 'type',
    // A fixed domain, so filtering out an empty category cannot re-index the
    // palette and hand Done the color Active had. Shared with the cumulative
    // flow chart beneath it.
    scale: { color: getWorkStatusCategoryColorScale(token) },
    // Shares a row with `BreakdownPie`, so it takes the same plot area plus the
    // same legend allowance — this chart always has a legend.
    height: BREAKDOWN_CHART_HEIGHT + BREAKDOWN_LEGEND_HEIGHT,
    // No fixed `width`: the card sizes it, and `autoFit` plus the remount hook
    // keep it following the column as the facts rail opens and closes.
    autoFit: true,
    label: {
      text: (datum: any) => {
        const total = progress?.total || 1
        return `${datum.count} (${((datum.count / total) * 100).toFixed(0)}%)`
      },
    },
    tooltip: {
      title: 'type',
    },
    // Below, not beside: G2 takes legend space out of the plot area, so a
    // right-hand legend shrinks the donut out of step with the breakdown pies
    // sharing its row.
    legend: {
      color: {
        title: false,
        position: 'bottom',
        layout: { justifyContent: 'center' },
      },
    },
  } as any // this is a hack to fix typescript error. Should be as PieConfig

  // TODO: fix typescript error on Pie component
  return (
    <ChartCard
      title="Progress"
      tooltip={PROGRESS_TOOLTIP}
      loading={isLoading}
      skeletonHeight={BREAKDOWN_CHART_HEIGHT}
    >
      <div ref={ref}>
        <Pie key={renderKey} {...config} />
      </div>
    </ChartCard>
  )
}

export default WorkItemProgressPieChart
