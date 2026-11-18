'use client'

import { Line } from '@/src/components/common/charts'
import useTheme from '@/src/components/contexts/theme'
import { useChartRemountOnResize } from '@/src/hooks'
import { KpiSummaryDto, KpiWindowKind } from '@/src/services/api/wayd.schemas'
import { parseCalendarDate } from '@/src/utils/calendar-date'
import { instantTime } from '@/src/utils/instant'
import { Card, Flex, Typography } from 'antd'
import { FC } from 'react'
import { KpiHealthTag, KpiTrendTag } from '../kpi-status-tags'
import {
  describeTarget,
  describeWindow,
  formatKpiValue,
  kpiHeadline,
} from './kpi-format'

const { Text } = Typography

const SPARKLINE_HEIGHT = 44

export interface ScorecardKpiCardProps {
  kpi: KpiSummaryDto
  onOpen: (kpi: KpiSummaryDto) => void
}

/**
 * One KPI on a scorecard: its headline reading, health and trend, what it is
 * measured against, and a line of its recent values. Opens the KPI on click.
 */
const ScorecardKpiCard: FC<ScorecardKpiCardProps> = ({ kpi, onOpen }) => {
  const { antDesignChartsTheme, token } = useTheme()
  const { ref, renderKey } = useChartRemountOnResize()
  const headline = kpiHeadline(kpi)
  const { goal } = kpi

  const sparkline = {
    theme: antDesignChartsTheme,
    autoFit: true,
    height: SPARKLINE_HEIGHT,
    data: kpi.recentValues.map((p) => ({
      at: p.measuredAt
        ? new Date(instantTime(p.measuredAt))
        : parseCalendarDate(p.periodEnd).toDate(),
      value: p.value,
    })),
    xField: 'at',
    yField: 'value',
    axis: false,
    legend: false,
    tooltip: false,
    margin: 2,
    padding: 0,
    scale: { y: { nice: true } },
    style: { lineWidth: 2, stroke: token.colorPrimary },
  }

  return (
    <Card
      size="small"
      hoverable
      onClick={() => onOpen(kpi)}
      role="button"
      tabIndex={0}
      onKeyDown={(e) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault()
          onOpen(kpi)
        }
      }}
      aria-label={`KPI ${kpi.key}: ${kpi.name}`}
      style={{ height: '100%' }}
    >
      <Flex vertical gap={8}>
        <Flex justify="space-between" align="flex-start" gap={8}>
          <Text
            strong
            ellipsis={{ tooltip: kpi.name }}
            style={{ fontSize: 14 }}
          >
            {kpi.name}
          </Text>
          <KpiHealthTag health={kpi.reading.health} />
        </Flex>

        <Flex vertical gap={0}>
          <Text type="secondary" style={{ fontSize: 12 }}>
            {kpi.window.kind === KpiWindowKind.PerPeriod
              ? headline.label
              : `${headline.label} · ${describeWindow(kpi.window)}`}
          </Text>
          <Flex align="baseline" gap={8} wrap>
            <Text style={{ fontSize: 22, fontWeight: 600, lineHeight: 1.2 }}>
              {headline.text}
            </Text>
            <KpiTrendTag trend={kpi.reading.trend} />
          </Flex>
        </Flex>

        <div ref={ref} style={{ minHeight: SPARKLINE_HEIGHT }}>
          {kpi.recentValues.length > 1 ? (
            <Line key={renderKey} {...sparkline} />
          ) : (
            <Text type="secondary" style={{ fontSize: 12 }}>
              {kpi.recentValues.length === 0
                ? 'Not measured yet'
                : 'One measurement so far'}
            </Text>
          )}
        </div>

        <Flex justify="space-between" gap={8} wrap>
          <Text type="secondary" style={{ fontSize: 12 }}>
            Baseline{' '}
            {formatKpiValue(goal.baselineValue, kpi.prefix, kpi.suffix)}
          </Text>
          <Text type="secondary" style={{ fontSize: 12 }}>
            Target {describeTarget(goal, kpi.prefix, kpi.suffix)}
          </Text>
        </Flex>
      </Flex>
    </Card>
  )
}

export default ScorecardKpiCard
