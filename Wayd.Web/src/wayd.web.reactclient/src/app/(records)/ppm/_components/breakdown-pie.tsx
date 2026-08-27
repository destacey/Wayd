'use client'

import { WaydEmpty } from '@/src/components/common'
import { ChartCard } from '@/src/components/common/metrics'
import useTheme from '@/src/components/contexts/theme'
import { softenChartColor } from '@/src/utils'
import type { PieConfig } from '@ant-design/charts'
import { theme } from 'antd'
import dynamic from 'next/dynamic'

const Pie = dynamic(
  () => import('@ant-design/charts').then((mod) => mod.Pie) as any,
  { ssr: false },
)

const CHART_HEIGHT = 280

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
}: BreakdownPieProps) => {
  const { antDesignChartsTheme } = useTheme()
  const { token } = theme.useToken()

  const total = data.reduce((sum, d) => sum + d.count, 0)
  const hasExplicitColors = data.length > 0 && data.every((d) => d.color)

  const config: PieConfig = {
    theme: antDesignChartsTheme,
    data,
    angleField: 'count',
    colorField: 'type',
    ...(hasExplicitColors && {
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
    height: CHART_HEIGHT,
    // Labels sit on the slices rather than outside them: leader lines from a
    // ring of small slices collide with each other, which is what an outside
    // label does on a breakdown with a long tail. overlapDodgeY then nudges
    // any that would still overlap.
    label: {
      text: (d: BreakdownDatum) =>
        `${d.type}\n ${d.count} (${Math.round((d.count / total) * 100)}%)`,
      style: {
        fill: token.colorTextLightSolid,
        fontWeight: 500,
      },
      transform: [{ type: 'overlapDodgeY' }],
    },
    // The label already names the category, so a legend would repeat it.
    legend: false,
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
    <ChartCard title={title} tooltip={tooltip} loading={isLoading}>
      {total === 0 ? (
        <WaydEmpty message={emptyMessage} />
      ) : (
        <Pie {...(config as any)} />
      )}
    </ChartCard>
  )
}

export default BreakdownPie
