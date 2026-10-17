'use client'

import { InfoCircleOutlined } from '@ant-design/icons'
import {
  Alert,
  Flex,
  Segmented,
  Select,
  Space,
  Spin,
  Table,
  Tag,
  Typography,
} from 'antd'
import type { TableColumnsType } from 'antd'
import { FC, useState } from 'react'
import {
  SizingMethod,
  TeamCadenceMetricsDto,
  TeamCadencePeriodDto,
} from '@/src/services/wayd-api'
import { useGetTeamCadenceMetricsQuery } from '@/src/store/features/organizations/team-api'
import { useLocalStorageState } from '@/src/hooks'
import { sizingMethodLabel, sizingMethodMeasure } from '@/src/utils'
import { formatReportingCadence } from '../../scheduling/reporting-cadence-fields'
import { METRIC_CARD_FLEX, MetricCard } from '../../metrics'
import WaydEmpty from '../../wayd-empty'
import WaydTooltip from '../../wayd-tooltip'
import {
  formatVelocity,
  formatVelocityAverage,
  formatVelocityRange,
  MAX_PERIOD_WINDOW,
  MIN_PERIOD_WINDOW,
  NO_VALUE,
  PERIOD_NOTE_DESCRIPTIONS,
  periodCountText,
  periodDates,
  periodNote,
  periodVelocity,
  showsCount as showsCountOf,
  VELOCITY_TOOLTIP,
  velocitySummary,
} from './team-cadence-metrics-formatting'
import TeamCadenceVelocityChart from './team-cadence-velocity-chart'

const { Title, Text } = Typography

const COUNT = 'Count'

const WINDOW_OPTIONS = Array.from(
  { length: MAX_PERIOD_WINDOW - MIN_PERIOD_WINDOW + 1 },
  (_, i) => MIN_PERIOD_WINDOW + i,
)

export interface TeamCadenceMetricsReportViewProps {
  metrics?: TeamCadenceMetricsDto
  isLoading: boolean
  error?: unknown
  /** The window shown: the viewer's choice, or the organization's until they make one. */
  periodWindow?: number
  onPeriodWindowChange: (periodWindow: number) => void
  byCount: boolean
  onByCountChange: (byCount: boolean) => void
}

export const TeamCadenceMetricsReportView: FC<
  TeamCadenceMetricsReportViewProps
> = ({
  metrics,
  isLoading,
  error,
  periodWindow,
  onPeriodWindowChange,
  byCount,
  onByCountChange,
}) => {
  const sizingMethod = metrics?.sizingMethod ?? SizingMethod.Count
  const isCountSized = sizingMethod === SizingMethod.Count
  const showsCount = showsCountOf(metrics, byCount)
  const unitLabel = sizingMethodLabel(sizingMethod)
  const measure = sizingMethodMeasure(
    showsCount ? SizingMethod.Count : sizingMethod,
  )
  const summary = metrics ? velocitySummary(metrics, byCount) : undefined
  const otherSizing =
    metrics?.periods.filter(
      (p) => p.inAverages && !p.inEstimateAverages && !isCountSized,
    ) ?? []

  const columns: TableColumnsType<TeamCadencePeriodDto> = [
    {
      title: 'Period',
      key: 'dates',
      render: (_, period) => periodDates(period),
    },
    {
      title: 'Completed',
      key: 'velocity',
      align: 'right',
      render: (_, period) => {
        const velocity = metrics
          ? periodVelocity(period, metrics, byCount)
          : null
        if (velocity !== null) return formatVelocity(velocity)
        return (
          <WaydTooltip
            title={`Measured in ${sizingMethodMeasure(period.sizingMethod)}, the team's sizing method at the time. Switch to Count to compare it.`}
            helpCursor
          >
            <Text type="secondary">{NO_VALUE}</Text>
          </WaydTooltip>
        )
      },
    },
    ...(showsCount
      ? []
      : [
          {
            title: 'Unestimated',
            key: 'unestimated',
            align: 'right' as const,
            render: (_: unknown, period: TeamCadencePeriodDto) =>
              period.unestimatedCount,
          },
        ]),
    {
      title: '',
      key: 'note',
      render: (_, period) => {
        const note = periodNote(period)
        return note ? (
          <WaydTooltip title={PERIOD_NOTE_DESCRIPTIONS[note]} helpCursor>
            <Tag>{note}</Tag>
          </WaydTooltip>
        ) : null
      },
    },
  ]

  const content = (() => {
    if (error)
      return (
        <Alert
          type="error"
          showIcon
          title="The team's cadence velocity could not be loaded."
        />
      )
    if (!metrics) return <Spin />
    if (metrics.periods.length === 0)
      return (
        <WaydEmpty message="The team has no cadence. Set one on its operating model to report velocity by period." />
      )

    const averaged = metrics.periods.filter((p) => p.inAverages).length

    return (
      <>
        {averaged < metrics.periodWindow && (
          <Text type="secondary">
            {`Averaged over ${periodCountText(summary?.periods ?? 0)}: the team has fewer than ${metrics.periodWindow} ended, whole periods.`}
          </Text>
        )}
        <Flex gap="small" wrap>
          <MetricCard
            title="Average Velocity"
            value={formatVelocityAverage(summary)}
            secondaryValue={formatVelocityRange(summary)}
            tooltip={`${VELOCITY_TOOLTIP} Measured in ${measure}${summary ? `, over ${periodCountText(summary.periods)}` : ''}.`}
            cardStyle={METRIC_CARD_FLEX}
          />
        </Flex>
        {otherSizing.length > 0 && !showsCount && (
          <Alert
            type="info"
            showIcon
            title={`${otherSizing.length === 1 ? '1 period was' : `${otherSizing.length} periods were`} measured in another sizing method`}
            description={`The team has changed its sizing method since. Estimates in different units are never averaged, so the ${unitLabel} average leaves ${otherSizing.length === 1 ? 'that period' : 'those periods'} out. Switch to Count to compare every period.`}
          />
        )}
        <TeamCadenceVelocityChart
          metrics={metrics}
          byCount={byCount}
          measure={measure}
        />
        <Table<TeamCadencePeriodDto>
          size="small"
          rowKey="start"
          columns={columns}
          dataSource={metrics.periods}
          pagination={false}
        />
      </>
    )
  })()

  return (
    <Flex vertical gap="middle">
      <Space>
        <Title level={4} style={{ margin: 0 }}>
          Velocity
        </Title>
        <WaydTooltip
          title={`${VELOCITY_TOOLTIP} The current period and periods cut short by an operating model change are shown but not averaged.`}
          helpCursor
        >
          <InfoCircleOutlined />
        </WaydTooltip>
        {metrics?.cadenceWeeks && (
          <Text type="secondary">
            {formatReportingCadence(metrics.cadenceWeeks)}
          </Text>
        )}
      </Space>

      <Flex gap="middle" wrap align="center">
        <Space>
          <Text type="secondary">Over the last</Text>
          <Select<number>
            aria-label="Period window"
            value={periodWindow ?? metrics?.periodWindow}
            loading={isLoading}
            onChange={onPeriodWindowChange}
            popupMatchSelectWidth={false}
            options={WINDOW_OPTIONS.map((value) => ({
              value,
              label:
                value === metrics?.defaultPeriodWindow
                  ? `${value} periods (default)`
                  : `${value} periods`,
            }))}
          />
        </Space>
        <WaydTooltip
          title={
            isCountSized
              ? 'This team sizes by count, so its velocity counts work items.'
              : `Switch between summing ${sizingMethodMeasure(sizingMethod)}, the team's sizing method, and counting work items`
          }
        >
          <Segmented<string>
            options={isCountSized ? [COUNT] : [unitLabel, COUNT]}
            value={showsCount ? COUNT : unitLabel}
            disabled={isCountSized}
            onChange={(value) => onByCountChange(value === COUNT)}
          />
        </WaydTooltip>
      </Flex>

      {content}
    </Flex>
  )
}

export interface TeamCadenceMetricsReportProps {
  teamId: string
}

/**
 * A Kanban team's velocity over its reporting cadence. It opens at the
 * organization's window every time: another range is for this visit only.
 */
export const TeamCadenceMetricsReport: FC<TeamCadenceMetricsReportProps> = ({
  teamId,
}) => {
  const [periodWindow, setPeriodWindow] = useState<number | undefined>()
  // Shared with the sprint metrics, so both stay in the unit last chosen.
  const [byCount, setByCount] = useLocalStorageState(
    'sprint-overview-by-count',
    false,
  )

  const { data, isFetching, error } = useGetTeamCadenceMetricsQuery(
    { teamId, periodWindow },
    { skip: !teamId },
  )

  return (
    <TeamCadenceMetricsReportView
      metrics={data}
      isLoading={isFetching}
      error={error}
      periodWindow={periodWindow}
      onPeriodWindowChange={setPeriodWindow}
      byCount={byCount}
      onByCountChange={setByCount}
    />
  )
}
