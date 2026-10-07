'use client'

import { ChartCard } from '@/src/components/common/metrics'
import WaydEmpty from '@/src/components/common/wayd-empty'
import useTheme from '@/src/components/contexts/theme'
import { useChartRemountOnResize } from '@/src/hooks'
import { Flex } from 'antd'
import dynamic from 'next/dynamic'
import { FC } from 'react'
import { BurnChartPoint, BurnSeries } from './sprint-burn-series'

const Line = dynamic(
  () => import('@ant-design/charts').then((mod) => mod.Line) as any,
  { ssr: false },
)

const CHART_HEIGHT = 260
const COMPACT_HEIGHT = 180

export interface BurnChartProps {
  title: string
  tooltip: string
  data: BurnChartPoint[]
  series: BurnSeries[]
  colors: string[]
  timeZone: string
  measure: string
  /** The sprint's span, which both charts share so their days line up; a running sprint's lines stop at now. */
  start: Date
  end: Date
  isLoading: boolean
  /** A small chart for a summary card: shorter, with no axis title. */
  compact?: boolean
}

/** A burn-up or burn-down chart over a sprint's span, read in the team's zone. */
const BurnChart: FC<BurnChartProps> = ({
  title,
  tooltip,
  data,
  series,
  colors,
  timeZone,
  measure,
  start,
  end,
  isLoading,
  compact = false,
}) => {
  const height = compact ? COMPACT_HEIGHT : CHART_HEIGHT
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
    height,
    data,
    xField: 'at',
    yField: 'value',
    colorField: 'series',
    shapeField: 'shape',
    scale: {
      x: { type: 'time', domain: [start, end] },
      shape: { type: 'identity' },
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
      y: { title: compact ? false : measure, gridStrokeOpacity: 0.2 },
    },
    // A compact chart drops G2's 16px margin and draws its legend on the title
    // line, so the plot keeps the card's height. The top margin is half a label,
    // so the top tick's label is not clipped.
    ...(compact ? { margin: 0, marginTop: 10 } : {}),
    legend: compact
      ? false
      : {
          color: {
            layout: { justifyContent: 'center' },
            itemMarker: 'square',
          },
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
      title={
        compact ? (
          <Flex justify="space-between" align="center" gap={12} wrap>
            <span>{title}</span>
            <Flex gap={12}>
              {series.map((name, i) => (
                <Flex key={name} align="center" gap={4}>
                  <span
                    aria-hidden
                    style={{
                      width: 8,
                      height: 8,
                      borderRadius: 2,
                      background: colors[i],
                    }}
                  />
                  <span style={{ fontSize: 12 }}>{name}</span>
                </Flex>
              ))}
            </Flex>
          </Flex>
        ) : (
          title
        )
      }
      tooltip={tooltip}
      loading={isLoading}
      skeletonHeight={height}
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

export default BurnChart
