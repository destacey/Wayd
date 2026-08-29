'use client'

import { FC } from 'react'
import dynamic from 'next/dynamic'
const Column = dynamic(
  () => import('@ant-design/charts').then((mod) => mod.Column) as any,
  { ssr: false },
)
import { ColumnConfig } from '@ant-design/charts'
import { Flex } from 'antd'
import { ChartSkeleton } from '@/src/components/common/metrics'
import WaydEmpty from '../wayd-empty'
import useTheme from '../../contexts/theme'
import { WorkItemListDto } from '@/src/services/wayd-api'

export interface CycleTimeAnalysisChartProps {
  workItems: WorkItemListDto[]
  isLoading?: boolean
}

interface ChartDataPoint {
  storyPointCategory: string
  averageCycleTime: number
  count: number
  sortOrder: number
}

export const CycleTimeAnalysisChart: FC<CycleTimeAnalysisChartProps> = ({
  workItems,
  isLoading = false,
}) => {
  const { antDesignChartsTheme, token } = useTheme()

  const chartData = (() => {
    // Handle empty work items
    if (workItems.length === 0) {
      return []
    }

    // Group work items by story points
    const groupedByStoryPoints = workItems.reduce(
      (acc, item) => {
        const key =
          item.storyPoints !== undefined && item.storyPoints !== null
            ? item.storyPoints.toString()
            : 'No Story Points'

        if (!acc[key]) {
          acc[key] = []
        }
        acc[key].push(item)
        return acc
      },
      {} as Record<string, WorkItemListDto[]>,
    )

    // Calculate average cycle time for each story point group
    const data: ChartDataPoint[] = Object.entries(groupedByStoryPoints).map(
      ([storyPointKey, items]) => {
        const totalCycleTime = items.reduce(
          (sum, item) => sum + (item.cycleTime || 0),
          0,
        )
        const averageCycleTime = totalCycleTime / items.length

        // Determine sort order (numeric story points first, then "No Story Points")
        const isNoStoryPoints = storyPointKey === 'No Story Points'
        const sortOrder = isNoStoryPoints
          ? Number.MAX_SAFE_INTEGER
          : parseFloat(storyPointKey)

        return {
          storyPointCategory: storyPointKey,
          averageCycleTime: Math.round(averageCycleTime * 100) / 100, // Round to 2 decimal places
          count: items.length,
          sortOrder,
        }
      },
    )

    // Sort by story points (numeric first, then "No Story Points" at the end)
    const sortedData = data.sort((a, b) => a.sortOrder - b.sortOrder)

    // Calculate overall average cycle time
    const totalCycleTime = workItems.reduce(
      (sum, item) => sum + (item.cycleTime || 0),
      0,
    )
    const overallAverage = totalCycleTime / workItems.length

    // Add "Overall" category at the end
    sortedData.push({
      storyPointCategory: 'Overall',
      averageCycleTime: Math.round(overallAverage * 100) / 100,
      count: workItems.length,
      sortOrder: Number.MAX_SAFE_INTEGER + 1,
    })

    return sortedData
  })()

  const config = {
    theme: antDesignChartsTheme,
    data: chartData,
    xField: 'storyPointCategory',
    yField: 'averageCycleTime',
    label: {
      text: (datum: ChartDataPoint) => `${datum.averageCycleTime.toFixed(2)}`,
      position: 'top' as const,
      style: {
        dy: -20,
      },
    },
    tooltip: {
      title: 'Story Points',
      items: [
        {
          name: 'Average Cycle Time',
          field: 'averageCycleTime',
          valueFormatter: (value: number) => `${value} days`,
        },
        {
          name: 'Work Items',
          field: 'count',
        },
      ],
    },
    axis: {
      x: {
        title: 'Story Points',
      },
      y: {
        title: 'Average Cycle Time (Days)',
      },
    },
    style: {
      fill: (datum: ChartDataPoint) => {
        if (datum.storyPointCategory === 'Overall') {
          return token.colorSuccess
        }
        if (datum.storyPointCategory === 'No Story Points') {
          return token.colorWarning
        }
        return token.colorPrimary
      },
    },
  } as ColumnConfig

  if (isLoading) {
    return <ChartSkeleton />
  }

  if (chartData.length === 0) {
    return <WaydEmpty message="No data available for chart" />
  }

  return (
    <Flex style={{ maxHeight: 350 }}>
      <Column {...(config as any)} />
    </Flex>
  )
}
