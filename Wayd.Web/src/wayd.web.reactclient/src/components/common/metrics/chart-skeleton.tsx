'use client'

import { DotChartOutlined } from '@ant-design/icons'
import { Flex, Skeleton } from 'antd'

export interface ChartSkeletonProps {
  /** Match the chart's `height`, so the card does not resize when data lands. */
  height?: number
}

/**
 * Placeholder for a loading chart.
 *
 * A chart-shaped block rather than the paragraph skeleton an antd `Card` draws
 * for `loading`: lines of fake text promise a chart is a list, and the card
 * jumps when the real plot replaces them.
 */
const ChartSkeleton = ({ height }: ChartSkeletonProps) => (
  <Flex justify="center" align="center" style={{ height: height ?? '100%' }}>
    <Skeleton.Node active>
      <DotChartOutlined style={{ fontSize: 40 }} />
    </Skeleton.Node>
  </Flex>
)

export default ChartSkeleton
