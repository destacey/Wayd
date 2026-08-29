'use client'

import { ChartCard } from '@/src/components/common/metrics'
import useTheme from '@/src/components/contexts/theme'
import { PlanningIntervalTeamPredictabilityDto } from '@/src/services/wayd-api'
import { useChartRemountOnResize } from '@/src/hooks'
import dynamic from 'next/dynamic'

const Radar = dynamic(
  () => import('@ant-design/charts').then((mod) => mod.Radar) as any,
  { ssr: false },
)

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
  const { ref, renderKey } = useChartRemountOnResize()

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
