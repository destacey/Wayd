'use client'

import { ChartCard } from '@/src/components/common/metrics'
import useTheme from '@/src/components/contexts/theme'
import { PlanningIntervalObjectiveListDto } from '@/src/services/wayd-api'
import {
  getObjectiveStatusColor,
  getSemanticChartColor,
  softenChartColor,
} from '@/src/utils'
import dynamic from 'next/dynamic'
import type { PieConfig } from '@ant-design/charts'
import { theme } from 'antd'

const Pie = dynamic(
  () => import('@ant-design/charts').then((mod) => mod.Pie) as any,
  { ssr: false },
)

export interface ObjectiveStatusChartProps {
  objectivesData: PlanningIntervalObjectiveListDto[]
  embedded?: boolean
  height?: number
}

interface ObjectiveStatusChartDataItem {
  type: string
  count: number
}

const OBJECTIVE_STATUS_ORDER = [
  'Not Started',
  'In Progress',
  'Completed',
  'Missed',
  'Canceled',
]

const ObjectiveStatusChart = (props: ObjectiveStatusChartProps) => {
  const { antDesignChartsTheme } = useTheme()
  const { token } = theme.useToken()

  if (!props.objectivesData || props.objectivesData.length === 0) return null

  const groupedStatusData = props.objectivesData.reduce(
    (acc, objective) => {
      const status = objective.status?.name ?? 'Unknown'
      acc[status] = acc[status] ? acc[status] + 1 : 1
      return acc
    },
    {} as Record<string, number>,
  )

  const data: ObjectiveStatusChartDataItem[] = Object.entries(groupedStatusData)
    .map(([status, count]) => ({ type: status, count }))
    .sort((a, b) => {
      const aIndex = OBJECTIVE_STATUS_ORDER.indexOf(a.type)
      const bIndex = OBJECTIVE_STATUS_ORDER.indexOf(b.type)
      const normalizedAIndex = aIndex === -1 ? Number.MAX_SAFE_INTEGER : aIndex
      const normalizedBIndex = bIndex === -1 ? Number.MAX_SAFE_INTEGER : bIndex
      return normalizedAIndex - normalizedBIndex
    })

  const statusDomain = data.map((item) => item.type)
  const statusRange = data.map((item) =>
    softenChartColor(
      getSemanticChartColor(getObjectiveStatusColor(item.type), token),
      token.colorBgContainer,
    ),
  )
  const tooltipBounding =
    typeof window !== 'undefined'
      ? {
          x: 0,
          y: 0,
          width: window.innerWidth,
          height: window.innerHeight,
        }
      : undefined
  const total = data.reduce((acc, x) => acc + x.count, 0)
  const config: PieConfig = {
    theme: antDesignChartsTheme,
    data,
    angleField: 'count',
    colorField: 'type',
    scale: {
      color: {
        domain: statusDomain,
        range: statusRange,
      },
    },
    autoFit: true,
    height: props.height ?? 280,
    padding: props.embedded ? 0 : 'auto',
    label: {
      text: (d: any) =>
        props.embedded
          ? `${d.count} (${Math.round((d.count / total) * 100)}%)`
          : `${d.type}\n ${d.count} (${Math.round((d.count / total) * 100)}%)`,
      style: {
        fill: token.colorTextLightSolid,
        fontWeight: 500,
      },
      transform: [
        {
          type: 'overlapDodgeY',
        },
      ],
    },
    legend: false,
    interaction: {
      tooltip: {
        mount: 'body',
        ...(props.embedded ? { position: 'top' as const } : {}),
        ...(tooltipBounding ? { bounding: tooltipBounding } : {}),
      },
    },
    tooltip: {
      title: () => 'Objectives',
      items: [
        (d: ObjectiveStatusChartDataItem) => ({
          name: d.type,
          value: `${d.count} (${Math.round((d.count / total) * 100)}%)`,
        }),
      ],
    } as any,
  }

  return (
    <ChartCard title="Objectives By Status" embedded={props.embedded}>
      <Pie {...(config as any)} />
    </ChartCard>
  )
}

export default ObjectiveStatusChart
