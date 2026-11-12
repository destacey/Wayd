'use client'

import type { ComponentType } from 'react'
import dynamic from 'next/dynamic'
import { withTextTooltip } from './chart-tooltip'

/** Wraps an `@ant-design/charts` component so every config it receives goes through `withTextTooltip`. */
const withTextTooltipChart = (
  Chart: ComponentType<any>,
  name: string,
): ComponentType<any> => {
  const TextTooltipChart = (props: any) => <Chart {...withTextTooltip(props)} />
  TextTooltipChart.displayName = name
  return TextTooltipChart
}

// The only place the client loads @ant-design/charts components: lint rejects any other import, so
// every chart renders its tooltip text through renderChartTooltip. Each loads in the browser only.

/** A pie or donut chart from `@ant-design/charts`, with text-only tooltips. */
export const Pie = dynamic(
  () =>
    import('@ant-design/charts').then((mod) =>
      withTextTooltipChart(mod.Pie, 'TextTooltipPie'),
    ),
  { ssr: false },
)

/** A line chart from `@ant-design/charts`, with text-only tooltips. */
export const Line = dynamic(
  () =>
    import('@ant-design/charts').then((mod) =>
      withTextTooltipChart(mod.Line, 'TextTooltipLine'),
    ),
  { ssr: false },
)

/** A column chart from `@ant-design/charts`, with text-only tooltips. */
export const Column = dynamic(
  () =>
    import('@ant-design/charts').then((mod) =>
      withTextTooltipChart(mod.Column, 'TextTooltipColumn'),
    ),
  { ssr: false },
)

/** An area chart from `@ant-design/charts`, with text-only tooltips. */
export const Area = dynamic(
  () =>
    import('@ant-design/charts').then((mod) =>
      withTextTooltipChart(mod.Area, 'TextTooltipArea'),
    ),
  { ssr: false },
)

/** A radar chart from `@ant-design/charts`, with text-only tooltips. */
export const Radar = dynamic(
  () =>
    import('@ant-design/charts').then((mod) =>
      withTextTooltipChart(mod.Radar, 'TextTooltipRadar'),
    ),
  { ssr: false },
)
