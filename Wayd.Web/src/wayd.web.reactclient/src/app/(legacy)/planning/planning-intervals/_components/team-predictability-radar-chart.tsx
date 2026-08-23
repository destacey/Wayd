'use client'

import { ChartCard } from '@/src/components/common/metrics'
import useTheme from '@/src/components/contexts/theme'
import { PlanningIntervalTeamPredictabilityDto } from '@/src/services/wayd-api'
import dynamic from 'next/dynamic'
import { useEffect, useRef, useState } from 'react'

const Radar = dynamic(
  () => import('@ant-design/charts').then((mod) => mod.Radar) as any,
  { ssr: false },
)

// @ant-design/charts v2 has a known issue where Radar doesn't always reflow
// when its container resizes (autoFit's ResizeObserver fires but the polygon
// stays positioned for the original width, leaving it visually off-center).
// We watch the container ourselves and bump a key whenever the width changes
// by more than a small threshold, forcing a remount that re-runs the chart's
// initial layout against the current dimensions.
const WIDTH_CHANGE_THRESHOLD_PX = 8

const useRemountKeyOnResize = () => {
  const ref = useRef<HTMLDivElement | null>(null)
  const [renderKey, setRenderKey] = useState(0)
  const lastWidthRef = useRef(0)

  useEffect(() => {
    const node = ref.current
    if (!node) return

    const observer = new ResizeObserver((entries) => {
      const width = entries[0]?.contentRect.width ?? 0
      if (Math.abs(width - lastWidthRef.current) >= WIDTH_CHANGE_THRESHOLD_PX) {
        lastWidthRef.current = width
        setRenderKey((k) => k + 1)
      }
    })

    observer.observe(node)
    return () => observer.disconnect()
  }, [])

  return { ref, renderKey }
}

interface TeamPredictabilityRadarChartProps {
  teamPredictabilities?: PlanningIntervalTeamPredictabilityDto[]
  isLoading: boolean
}

const TeamPredictabilityRadarChart: React.FC<
  TeamPredictabilityRadarChartProps
> = ({
  teamPredictabilities,
  isLoading,
}: TeamPredictabilityRadarChartProps) => {
  const { antDesignChartsTheme } = useTheme()
  const { ref, renderKey } = useRemountKeyOnResize()

  const config = (() => {
    return {
      theme: antDesignChartsTheme,
      autoFit: true,
      height: 280,
      data: teamPredictabilities?.map((x) => ({
        team: x.team.name,
        predictability: x.predictability ?? 0,
      })),
      xField: 'team',
      yField: 'predictability',
      tooltip: {
        items: [
          {
            channel: 'y',
            valueFormatter: (value: any) => `${value.toFixed(0)}%`,
            name: 'Predictability',
          },
        ],
      },
      area: {
        style: {
          fillOpacity: 0.2,
        },
      },
      scale: {
        x: {
          padding: 0.5,
          align: 0,
        },
        y: {
          domainMin: 0,
          domainMax: 100,
          tickInterval: 20,
        },
      },
      axis: {
        x: {
          title: false,
          grid: true,
        },
        y: {
          gridAreaFill: 'rgba(0, 0, 0, 0.1)',
          label: true,
          title: false,
        },
      },
    } as any // this is a hack to fix typescript error. Should be as RadarConfig
  })()

  return (
    <ChartCard
      title="Team Predictability"
      loading={isLoading}
      cardStyle={{ height: '100%' }}
    >
      <div ref={ref}>
        <Radar key={renderKey} {...config} />
      </div>
    </ChartCard>
  )
}

export default TeamPredictabilityRadarChart
