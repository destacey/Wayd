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
import Link from 'next/link'
import { FC, useState } from 'react'
import {
  SizingMethod,
  TeamSprintMetricsDto,
  TeamSprintMetricsExcludedSprintDto,
  TeamSprintMetricsSprintDto,
} from '@/src/services/wayd-api'
import { useGetTeamSprintMetricsQuery } from '@/src/store/features/organizations/team-api'
import { useLocalStorageState } from '@/src/hooks'
import { sizingMethodLabel, sizingMethodMeasure } from '@/src/utils'
import { formatCalendarDate } from '@/src/utils/calendar-date'
import { METRIC_CARD_FLEX, MetricCard } from '../../metrics'
import WaydEmpty from '../../wayd-empty'
import WaydTooltip from '../../wayd-tooltip'
import {
  EXCLUSION_DESCRIPTIONS,
  EXCLUSION_LABELS,
  formatSprintMetric,
  formatSprintMetricAverage,
  formatSprintMetricRange,
  MAX_SPRINT_WINDOW,
  METRIC_TOOLTIPS,
  MIN_SPRINT_WINDOW,
  NO_VALUE,
  sprintCountText,
  SprintMetricKind,
  sprintMetricFigures,
  sprintMetricSummaries,
} from './team-sprint-metrics-formatting'

const { Title, Text } = Typography

const COUNT = 'Count'

const METRICS: { kind: SprintMetricKind; title: string }[] = [
  { kind: 'velocity', title: 'Velocity' },
  { kind: 'predictability', title: 'Predictability' },
  { kind: 'sayDo', title: 'Say/Do' },
]

const WINDOW_OPTIONS = Array.from(
  { length: MAX_SPRINT_WINDOW - MIN_SPRINT_WINDOW + 1 },
  (_, i) => MIN_SPRINT_WINDOW + i,
)

const sprintDates = (sprint: { start: string; end: string }) =>
  `${formatCalendarDate(sprint.start)} – ${formatCalendarDate(sprint.end)}`

export interface TeamSprintMetricsReportViewProps {
  metrics?: TeamSprintMetricsDto
  isLoading: boolean
  error?: unknown
  /** The window shown: the viewer's choice, or the organization's until they make one. */
  sprintWindow?: number
  onSprintWindowChange: (sprintWindow: number) => void
  byCount: boolean
  onByCountChange: (byCount: boolean) => void
}

export const TeamSprintMetricsReportView: FC<
  TeamSprintMetricsReportViewProps
> = ({
  metrics,
  isLoading,
  error,
  sprintWindow,
  onSprintWindowChange,
  byCount,
  onByCountChange,
}) => {
  const sizingMethod = metrics?.sizingMethod ?? SizingMethod.Count
  const isCountSized = sizingMethod === SizingMethod.Count
  const showsCount = byCount || isCountSized
  const unitLabel = sizingMethodLabel(sizingMethod)
  const measure = sizingMethodMeasure(
    showsCount ? SizingMethod.Count : sizingMethod,
  )

  const summaries = metrics
    ? sprintMetricSummaries(metrics, showsCount)
    : undefined
  const otherSizing =
    metrics?.sprints.filter((s) => !s.inEstimateAverages) ?? []

  const metricColumn = (kind: SprintMetricKind, title: string) => ({
    title,
    key: kind,
    align: 'right' as const,
    render: (_: unknown, sprint: TeamSprintMetricsSprintDto) => {
      const figures = sprintMetricFigures(sprint, showsCount)
      if (figures) return formatSprintMetric(kind, figures[kind])
      return (
        <WaydTooltip
          title={`Measured in ${sizingMethodMeasure(sprint.sizingMethod)}, the team's sizing method at the time. Switch to Count to compare it.`}
          helpCursor
        >
          <Text type="secondary">{NO_VALUE}</Text>
        </WaydTooltip>
      )
    },
  })

  const columns: TableColumnsType<TeamSprintMetricsSprintDto> = [
    {
      title: 'Sprint',
      key: 'name',
      render: (_, sprint) => (
        <Link href={`/work/sprints/${sprint.key}`}>{sprint.name}</Link>
      ),
    },
    {
      title: 'Dates',
      key: 'dates',
      render: (_, sprint) => sprintDates(sprint),
    },
    ...METRICS.map((m) => metricColumn(m.kind, m.title)),
  ]

  const excludedColumns: TableColumnsType<TeamSprintMetricsExcludedSprintDto> =
    [
      {
        title: 'Sprint',
        key: 'name',
        render: (_, sprint) => (
          <Link href={`/work/sprints/${sprint.key}`}>{sprint.name}</Link>
        ),
      },
      {
        title: 'Dates',
        key: 'dates',
        render: (_, sprint) => sprintDates(sprint),
      },
      {
        title: 'Left out because',
        key: 'reason',
        render: (_, sprint) => (
          <WaydTooltip title={EXCLUSION_DESCRIPTIONS[sprint.reason]} helpCursor>
            <Tag>{EXCLUSION_LABELS[sprint.reason]}</Tag>
          </WaydTooltip>
        ),
      },
    ]

  const content = (() => {
    if (error)
      return (
        <Alert
          type="error"
          showIcon
          title="The team's sprint metrics could not be loaded."
        />
      )
    if (!metrics) return <Spin />

    return (
      <>
        {metrics.sprints.length === 0 ? (
          <WaydEmpty message="No completed standard sprints with a commitment to measure yet" />
        ) : (
          <>
            {metrics.sprints.length < metrics.sprintWindow && (
              <Text type="secondary">
                {`Measured over ${metrics.sprints.length} ${metrics.sprints.length === 1 ? 'sprint' : 'sprints'}: the team has fewer than ${metrics.sprintWindow} usable among its most recent completed sprints.`}
              </Text>
            )}
            <Flex gap="small" wrap>
              {METRICS.map(({ kind, title }) => (
                <MetricCard
                  key={kind}
                  title={`Average ${title}`}
                  value={formatSprintMetricAverage(kind, summaries?.[kind])}
                  secondaryValue={formatSprintMetricRange(
                    kind,
                    summaries?.[kind],
                  )}
                  tooltip={`${METRIC_TOOLTIPS[kind]} Measured in ${measure}${summaries?.[kind] ? `, over ${sprintCountText(summaries[kind].sprints)}` : ''}.`}
                  cardStyle={METRIC_CARD_FLEX}
                />
              ))}
            </Flex>
            {!showsCount && otherSizing.length > 0 && (
              <Alert
                type="info"
                showIcon
                title={`${otherSizing.length === 1 ? '1 sprint was' : `${otherSizing.length} sprints were`} measured in another sizing method`}
                description={`The team has changed its sizing method since. Estimates in different units are never averaged, so the ${unitLabel} averages leave ${otherSizing.length === 1 ? 'that sprint' : 'those sprints'} out. Switch to Count to compare every sprint.`}
              />
            )}
            <Table<TeamSprintMetricsSprintDto>
              size="small"
              rowKey="id"
              columns={columns}
              dataSource={metrics.sprints}
              pagination={false}
            />
          </>
        )}
        {metrics.excluded.length > 0 && (
          <Flex vertical gap="small">
            <Text strong>Left out</Text>
            <Text type="secondary">
              Completed sprints passed over while finding the sprints above.
            </Text>
            <Table<TeamSprintMetricsExcludedSprintDto>
              size="small"
              rowKey="id"
              columns={excludedColumns}
              dataSource={metrics.excluded}
              pagination={false}
            />
          </Flex>
        )}
      </>
    )
  })()

  return (
    <Flex vertical gap="middle">
      <Space>
        <Title level={4} style={{ margin: 0 }}>
          Sprint Metrics
        </Title>
        <WaydTooltip
          title="The team's velocity, predictability and say/do over its most recent completed standard sprints, from the same scope each sprint reports on its own page. Non-standard sprints, sprints with incomplete history and sprints with nothing committed are left out."
          helpCursor
        >
          <InfoCircleOutlined />
        </WaydTooltip>
      </Space>

      <Flex gap="middle" wrap align="center">
        <Space>
          <Text type="secondary">Over the last</Text>
          <Select<number>
            aria-label="Sprint window"
            value={sprintWindow ?? metrics?.sprintWindow}
            loading={isLoading}
            onChange={onSprintWindowChange}
            popupMatchSelectWidth={false}
            options={WINDOW_OPTIONS.map((value) => ({
              value,
              label:
                value === metrics?.defaultSprintWindow
                  ? `${value} sprints (default)`
                  : `${value} sprints`,
            }))}
          />
        </Space>
        <WaydTooltip
          title={
            isCountSized
              ? 'This team sizes by count, so its metrics count work items.'
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

export interface TeamSprintMetricsReportProps {
  teamId: string
}

/**
 * A team's velocity, predictability and say/do over its last completed standard
 * sprints. It opens at the organization's sprint window every time: another
 * range is for this visit only, so two people looking at a team start from the
 * same figures.
 */
export const TeamSprintMetricsReport: FC<TeamSprintMetricsReportProps> = ({
  teamId,
}) => {
  const [sprintWindow, setSprintWindow] = useState<number | undefined>()
  // Shared with the sprint page, so both stay in the unit last chosen.
  const [byCount, setByCount] = useLocalStorageState(
    'sprint-overview-by-count',
    false,
  )

  const { data, isFetching, error } = useGetTeamSprintMetricsQuery(
    { teamId, sprintWindow },
    { skip: !teamId },
  )

  return (
    <TeamSprintMetricsReportView
      metrics={data}
      isLoading={isFetching}
      error={error}
      sprintWindow={sprintWindow}
      onSprintWindowChange={setSprintWindow}
      byCount={byCount}
      onByCountChange={setByCount}
    />
  )
}
