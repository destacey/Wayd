'use client'

import dynamic from 'next/dynamic'
import { WorkItemProgressDailyRollupDto } from '@/src/services/wayd-api'
import useTheme from '@/src/components/contexts/theme'
import { Card } from 'antd'

const Pie = dynamic(
  () => import('@ant-design/charts').then((mod) => mod.Pie) as any,
  { ssr: false },
)

export interface WorkItemProgressPieChartProps {
  progress: WorkItemProgressDailyRollupDto
  isLoading: boolean
}

const WorkItemProgressPieChart = ({
  progress,
  isLoading,
}: WorkItemProgressPieChartProps) => {
  const { antDesignChartsTheme } = useTheme()

  if (
    !progress ||
    (progress.proposed === 0 && progress.active === 0 && progress.done === 0)
  )
    return null

  const data = [
    { type: 'Proposed', count: progress.proposed },
    { type: 'Active', count: progress.active },
    { type: 'Done', count: progress.done },
  ]

  const config = {
    title: 'Progress',
    theme: antDesignChartsTheme,
    data: data,
    angleField: 'count',
    colorField: 'type',
    height: 250,
    width: 350,
    label: {
      text: (datum: any) => {
        return `${datum.count} (${((datum.count / progress.total) * 100).toFixed(0)}%)`
      },
    },
    tooltip: {
      title: 'type',
    },
    legend: {
      color: {
        title: false,
        position: 'right',
        rowPadding: 5,
      },
    },
  } as any // this is a hack to fix typescript error. Should be as PieConfig

  // TODO: fix typescript error on Pie component
  return (
    <Card size="small" loading={isLoading}>
      <Pie {...config} />
    </Card>
  )
}

export default WorkItemProgressPieChart
