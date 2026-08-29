'use client'

import { WaydEmpty } from '@/src/components/common'
import { ChartCard } from '@/src/components/common/metrics'
import useTheme from '@/src/components/contexts/theme'
import { useChartRemountOnResize } from '@/src/hooks'
import { softenChartColor } from '@/src/utils'
import type { PieConfig } from '@ant-design/charts'
import { theme } from 'antd'
import dynamic from 'next/dynamic'

const Pie = dynamic(
  () => import('@ant-design/charts').then((mod) => mod.Pie) as any,
  { ssr: false },
)

/** Plot area for a breakdown donut, legend excluded. */
export const BREAKDOWN_CHART_HEIGHT = 280

/**
 * Extra height for a legend, so the plot area above it stays the size it is on
 * a chart without one. G2 takes the legend out of the chart's height rather
 * than adding to it, which otherwise leaves the donut smaller and sitting
 * higher than its neighbours in the same row.
 *
 * Any chart sharing a row with these must add it too — see the progress pie.
 */
export const BREAKDOWN_LEGEND_HEIGHT = 32

export interface BreakdownDatum {
  type: string
  count: number
  /** Slice color. Omit to let the chart theme assign one from its palette. */
  color?: string
}

export interface BreakdownPieProps {
  title: string
  data: BreakdownDatum[]
  isLoading?: boolean
  /** Shown in place of the chart when nothing matches the current filter. */
  emptyMessage?: string
  /** Explains what the chart counts, on the title. */
  tooltip?: string
  /**
   * A fixed color scale over the breakdown's full vocabulary.
   *
   * Supply this where the categories are a known, closed set — dependency
   * health, work status. It pins each category's color whether or not it is
   * present, and switches the chart to a legend listing every option, so a
   * chart showing one value still names the alternatives.
   *
   * Omit it for open-ended breakdowns (strategic themes, work types): their
   * vocabulary is whatever the data holds, so the slice labels carry the names
   * instead and there is nothing a legend could add.
   */
  colorScale?: { domain: string[]; range: string[] }
}

/**
 * A labelled donut over one categorical breakdown of a record's children.
 *
 * Colors come from the active chart theme unless a datum names one, which the
 * status breakdown does so its slices agree with the status tags shown
 * everywhere else. Nothing here hardcodes a color: `color` arrives already
 * resolved from a theme token, and is softened against the card background the
 * way the planning interval charts are.
 */
const BreakdownPie = ({
  title,
  data,
  isLoading = false,
  emptyMessage = 'Nothing to show for the selected filters.',
  tooltip,
  colorScale,
}: BreakdownPieProps) => {
  const { antDesignChartsTheme } = useTheme()
  const { token } = theme.useToken()
  const { ref, renderKey } = useChartRemountOnResize()

  const total = data.reduce((sum, d) => sum + d.count, 0)
  const hasExplicitColors = data.length > 0 && data.every((d) => d.color)

  const config: PieConfig = {
    theme: antDesignChartsTheme,
    data,
    angleField: 'count',
    colorField: 'type',
    // A supplied scale wins: it covers the whole vocabulary, where per-datum
    // colors only cover what the data happens to contain.
    ...(colorScale
      ? { scale: { color: colorScale } }
      : hasExplicitColors && {
          scale: {
            color: {
              domain: data.map((d) => d.type),
              range: data.map((d) =>
                softenChartColor(d.color as string, token.colorBgContainer),
              ),
            },
          },
        }),
    autoFit: true,
    height: colorScale
      ? BREAKDOWN_CHART_HEIGHT + BREAKDOWN_LEGEND_HEIGHT
      : BREAKDOWN_CHART_HEIGHT,
    // Labels sit on the slices rather than outside them: leader lines from a
    // ring of small slices collide with each other, which is what an outside
    // label does on a breakdown with a long tail. overlapDodgeY then nudges
    // any that would still overlap.
    label: {
      text: (d: BreakdownDatum) =>
        colorScale
          ? `${d.count} (${Math.round((d.count / total) * 100)}%)`
          : `${d.type}\n ${d.count} (${Math.round((d.count / total) * 100)}%)`,
      style: {
        fill: token.colorTextLightSolid,
        fontWeight: 500,
      },
      transform: [{ type: 'overlapDodgeY' }],
    },
    // A fixed scale earns a legend: it names the categories the data does not
    // contain, which is the point of declaring the vocabulary up front. Without
    // one the slice label carries the name and a legend would only repeat it.
    //
    // Below, not beside: G2 takes legend space out of the plot area, so a
    // right-hand legend shrinks the donut and a chart with one no longer
    // matches a chart without one sitting next to it.
    legend: colorScale
      ? {
          color: {
            title: false,
            position: 'bottom',
            layout: { justifyContent: 'center' },
          },
        }
      : false,
    interaction: { tooltip: { mount: 'body' } },
    tooltip: {
      title: () => title,
      items: [
        (d: BreakdownDatum) => ({
          name: d.type,
          value: `${d.count} (${Math.round((d.count / total) * 100)}%)`,
        }),
      ],
    } as any,
  }

  return (
    <ChartCard
      title={title}
      tooltip={tooltip}
      loading={isLoading}
      skeletonHeight={BREAKDOWN_CHART_HEIGHT}
    >
      {/* One wrapper across every state, so the observer is attached before
          the data arrives — see `useChartRemountOnResize` for why `autoFit`
          alone leaves the canvas at whatever width it first measured. */}
      <div ref={ref}>
        {total === 0 ? (
          <WaydEmpty message={emptyMessage} />
        ) : (
          <Pie key={renderKey} {...(config as any)} />
        )}
      </div>
    </ChartCard>
  )
}

export default BreakdownPie
