'use client'

import type { ColumnConfig, LineConfig } from '@ant-design/charts'
import { theme, Typography } from 'antd'
import { FC } from 'react'
import useTheme from '@/src/components/contexts/theme'
import { useChartRemountOnResize } from '@/src/hooks'
import { DependencyAnalysisPeriodDto } from '@/src/services/wayd-api'
import {
  buildFlowData,
  buildOpenData,
  describeFlow,
  describeOpen,
  FlowDatum,
  OpenDatum,
} from './dependency-analysis-over-time-data'
import { Column, Line } from '@/src/components/common/charts'

const { Text } = Typography

const CHART_HEIGHT = 240

export interface DependencyAnalysisOverTimeProps {
  periods: DependencyAnalysisPeriodDto[]
}

/**
 * How the dependencies changed across the range, oldest on the left: how many were open at the end of each
 * period, and how many were created and closed in it.
 */
const DependencyAnalysisOverTime: FC<DependencyAnalysisOverTimeProps> = ({
  periods,
}) => {
  const { antDesignChartsTheme } = useTheme()
  const { token } = theme.useToken()
  const { ref: openRef, renderKey: openKey } = useChartRemountOnResize()
  const { ref: flowRef, renderKey: flowKey } = useChartRemountOnResize()

  const openConfig = {
    theme: antDesignChartsTheme,
    autoFit: true,
    height: CHART_HEIGHT,
    data: buildOpenData(periods),
    xField: 'period',
    yField: 'open',
    // The points take G2's palette unless told otherwise, which drifts from a customised primary colour.
    point: {
      shapeField: 'circle',
      sizeField: 3,
      style: { fill: token.colorPrimary, stroke: token.colorPrimary },
    },
    style: { stroke: token.colorPrimary, lineWidth: 2 },
    axis: {
      x: { title: 'Period starting', labelAutoHide: true },
      y: { title: 'Open dependencies' },
    },
    tooltip: {
      title: (datum: OpenDatum) => datum.dates,
      items: [{ name: 'Open at the end', field: 'open' }],
    },
  } as LineConfig

  const flowConfig = {
    theme: antDesignChartsTheme,
    autoFit: true,
    height: CHART_HEIGHT,
    data: buildFlowData(periods),
    xField: 'period',
    yField: 'count',
    colorField: 'series',
    group: true,
    scale: {
      color: {
        domain: ['Created', 'Closed'],
        range: [token.colorPrimary, token.colorSuccess],
      },
    },
    legend: {
      color: { layout: { justifyContent: 'center' }, itemMarker: 'square' },
    },
    style: { radiusTopLeft: 3, radiusTopRight: 3 },
    axis: {
      x: { title: 'Period starting', labelAutoHide: true },
      y: { title: 'Dependencies' },
    },
    tooltip: {
      title: (datum: FlowDatum) => datum.dates,
    },
  } as ColumnConfig

  return (
    <div>
      <Text strong>Open at the end of each period</Text>
      <div ref={openRef} role="img" aria-label={describeOpen(periods)}>
        <Line key={openKey} {...(openConfig as any)} />
      </div>
      <Text strong>Created and closed in each period</Text>
      <div ref={flowRef} role="img" aria-label={describeFlow(periods)}>
        <Column key={flowKey} {...(flowConfig as any)} />
      </div>
      <Text type="secondary" style={{ fontSize: 12 }}>
        A dependency closes when its predecessor is done, or is removed with the
        successor done or removed too. A period marked partial covers fewer days
        than the others.
      </Text>
    </div>
  )
}

export default DependencyAnalysisOverTime
