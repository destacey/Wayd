'use client'

import { InfoCircleOutlined } from '@ant-design/icons'
import {
  Alert,
  DatePicker,
  Flex,
  Segmented,
  Space,
  Spin,
  Tabs,
  Typography,
  theme,
} from 'antd'
import type { TimeRangePickerProps } from 'antd'
import dayjs, { Dayjs } from 'dayjs'
import { FC, ReactNode, useState } from 'react'
import {
  WorkDistributionDimension,
  WorkDistributionMeasure,
  TeamWorkDistributionDto,
  ThemeCounting,
  UnestimatedHandling,
} from '@/src/services/wayd-api'
import { useGetTeamWorkDistributionQuery } from '@/src/store/features/organizations/team-api'
import { isApiError } from '@/src/utils/problem-details'
import { METRIC_CARD_FLEX, MetricCard } from '../../metrics'
import WaydTooltip from '../../wayd-tooltip'
import WaydEmpty from '../../wayd-empty'
import WorkDistributionBreakdown from './work-distribution-breakdown'
import {
  DIMENSION_LABELS,
  MEASURE_LABELS,
  measureTooltip,
  formatAmount,
  formatShare,
  groupSwatches,
} from './work-distribution-formatting'
import WorkDistributionOverTime from './work-distribution-over-time'
import WorkDistributionTeamMatrix from './work-distribution-team-matrix'

const { Title, Text } = Typography
const { RangePicker } = DatePicker

type WorkDistributionView = 'breakdown' | 'teams' | 'time'

const DATE_FORMAT = 'YYYY-MM-DD'

/** A year with room for a leap day, both ends inclusive; the API rejects longer ranges. */
const MAX_RANGE_DAYS = 366

/** Completed work only, so ranges end yesterday: today is not over. */
const yesterday = () => dayjs().subtract(1, 'day').startOf('day')

const lastQuarter = (): [Dayjs, Dayjs] => {
  const start = dayjs()
    .startOf('month')
    .subtract((dayjs().month() % 3) + 3, 'month')
  return [start, start.add(2, 'month').endOf('month').startOf('day')]
}

const RANGE_PRESETS: TimeRangePickerProps['presets'] = [
  {
    label: 'Last 30 days',
    value: () => [yesterday().subtract(29, 'day'), yesterday()],
  },
  {
    label: 'Last 90 days',
    value: () => [yesterday().subtract(89, 'day'), yesterday()],
  },
  { label: 'Last quarter', value: lastQuarter },
  {
    label: 'Last 6 months',
    value: () => [yesterday().subtract(6, 'month').add(1, 'day'), yesterday()],
  },
  {
    label: 'Last 12 months',
    value: () => [yesterday().subtract(12, 'month').add(1, 'day'), yesterday()],
  },
]

/** The API's validation messages, when the request was rejected as invalid. */
const validationMessages = (error: unknown): string[] =>
  isApiError(error) && error.errors ? Object.values(error.errors).flat() : []

export interface WorkDistributionSettings {
  range: [Dayjs, Dayjs]
  dimension: WorkDistributionDimension
  measure: WorkDistributionMeasure
  unestimated: UnestimatedHandling
  themeCounting: ThemeCounting
}

export interface WorkDistributionReportViewProps {
  distribution?: TeamWorkDistributionDto
  isLoading: boolean
  error?: unknown
  settings: WorkDistributionSettings
  onSettingsChange: (settings: WorkDistributionSettings) => void
  /** A team of teams rolls up the teams beneath it, which the By Team view breaks out. */
  isTeamOfTeams: boolean
}

const SummaryCards: FC<{
  distribution?: TeamWorkDistributionDto
  measure: WorkDistributionMeasure
  loading: boolean
}> = ({ distribution, measure, loading }) => {
  const summary = distribution?.summary
  const card = (
    title: string,
    value: ReactNode,
    secondary?: ReactNode,
    tooltip?: string,
  ) => (
    <MetricCard
      key={title}
      title={title}
      value={value as number | string}
      secondaryValue={secondary}
      tooltip={tooltip}
      loading={loading}
      cardStyle={METRIC_CARD_FLEX}
    />
  )

  const noProject = card(
    'No Project',
    summary ? formatShare(summary.noProjectShare) : '',
    summary && `${summary.noProjectItems} items`,
    'Work whose item and parents link to no project.',
  )
  const linked = card(
    'Linked to a Project',
    summary ? formatShare(100 - summary.noProjectShare) : '',
    summary && `${summary.itemsCompleted - summary.noProjectItems} items`,
  )

  if (measure === WorkDistributionMeasure.StoryPoints) {
    const unestimated = summary
      ? summary.itemsInPointSizedTeams - summary.estimatedItems
      : 0
    const excluded = summary?.excludedTeams ?? []
    return (
      <>
        {card(
          'Story Points',
          summary
            ? formatAmount(summary.storyPoints + summary.filledStoryPoints)
            : '',
          summary &&
            (summary.filledItems > 0
              ? `Includes ${formatAmount(summary.filledStoryPoints)} filled in for ${summary.filledItems} items`
              : `${unestimated} unestimated items left out`),
        )}
        {card(
          'Estimated',
          summary && summary.itemsInPointSizedTeams > 0
            ? formatShare(
                (summary.estimatedItems / summary.itemsInPointSizedTeams) * 100,
              )
            : '—',
          summary &&
            `${summary.estimatedItems} of ${summary.itemsInPointSizedTeams} items in point-sized teams`,
          'Items with story points, among teams that size in story points. An estimate of 0 counts as estimated.',
        )}
        {card(
          'Teams Included',
          summary
            ? `${summary.teamsIncluded - excluded.length} of ${summary.teamsIncluded}`
            : '',
          excluded.length > 0
            ? `${excluded.map((t) => t.name).join(', ')} ${excluded.length === 1 ? "doesn't" : "don't"} size in points`
            : summary && 'Every team sizes in points',
        )}
        {linked}
        {noProject}
      </>
    )
  }

  return (
    <>
      {card(
        'Work Items Completed',
        summary ? formatAmount(summary.itemsCompleted) : '',
        measure === WorkDistributionMeasure.TeamEffort
          ? 'Every team counts, in its own sizing'
          : undefined,
      )}
      {card(
        'Story Points',
        summary ? formatAmount(summary.storyPoints) : '',
        summary &&
          `${summary.estimatedItems} of ${summary.itemsInPointSizedTeams} items estimated`,
      )}
      {card(
        'Teams Included',
        summary ? formatAmount(summary.teamsIncluded) : '',
      )}
      {linked}
      {noProject}
    </>
  )
}

export const WorkDistributionReportView: FC<
  WorkDistributionReportViewProps
> = ({
  distribution,
  isLoading,
  error,
  settings,
  onSettingsChange,
  isTeamOfTeams,
}) => {
  const { token } = theme.useToken()
  const [view, setView] = useState<WorkDistributionView>('breakdown')
  const set = (change: Partial<WorkDistributionSettings>) =>
    onSettingsChange({ ...settings, ...change })

  const { dimension, measure } = settings
  const overlapping =
    dimension === WorkDistributionDimension.StrategicTheme &&
    settings.themeCounting === ThemeCounting.CountFully
  const swatches = distribution ? groupSwatches(distribution.groups, token) : []
  const activeView = !isTeamOfTeams && view === 'teams' ? 'breakdown' : view

  const content = (() => {
    if (!distribution) return <Spin />
    if (distribution.summary.itemsCompleted === 0)
      return <WaydEmpty message="No completed work in this date range" />

    switch (activeView) {
      case 'teams':
        return (
          <WorkDistributionTeamMatrix
            distribution={distribution}
            dimension={dimension}
            measure={measure}
          />
        )
      case 'time':
        return (
          <WorkDistributionOverTime
            distribution={distribution}
            swatches={swatches}
            dimension={dimension}
          />
        )
      default:
        return (
          <WorkDistributionBreakdown
            distribution={distribution}
            swatches={swatches}
            dimension={dimension}
            measure={measure}
            overlapping={overlapping}
            showContributor={isTeamOfTeams}
          />
        )
    }
  })()

  return (
    <Flex vertical gap="middle">
      <Space>
        <Title level={4} style={{ margin: 0 }}>
          Work Distribution
        </Title>
        <WaydTooltip
          title={
            isTeamOfTeams
              ? 'Where the work completed by this team of teams, and every team beneath it, went. Each team counts toward the parent it had on the day the work was done.'
              : "Where the team's completed work went."
          }
          helpCursor
        >
          <InfoCircleOutlined />
        </WaydTooltip>
      </Space>

      <Flex gap="middle" wrap align="center">
        <Space>
          <Text type="secondary">Completed</Text>
          <RangePicker
            allowClear={false}
            value={settings.range}
            presets={RANGE_PRESETS}
            // Once one end is picked, the other can be at most a year away, as the API allows.
            disabledDate={(date, { from }) =>
              date.isAfter(dayjs(), 'day') ||
              (!!from && Math.abs(date.diff(from, 'day')) + 1 > MAX_RANGE_DAYS)
            }
            onChange={(range) =>
              range?.[0] && range[1] && set({ range: [range[0], range[1]] })
            }
          />
        </Space>
        <Space>
          <Text type="secondary">Measure</Text>
          <WaydTooltip title={measureTooltip(isTeamOfTeams)}>
            <Segmented<WorkDistributionMeasure>
              value={measure}
              onChange={(value) => set({ measure: value })}
              options={Object.values(WorkDistributionMeasure)
                // With one team there is nothing to combine: effort would repeat that team's own sizing.
                .filter(
                  (value) =>
                    isTeamOfTeams ||
                    value !== WorkDistributionMeasure.TeamEffort,
                )
                .map((value) => ({
                  value,
                  label: MEASURE_LABELS[value],
                }))}
            />
          </WaydTooltip>
        </Space>
      </Flex>

      <Flex gap="middle" wrap align="center">
        <Space>
          <Text type="secondary">Allocate by</Text>
          <Segmented<WorkDistributionDimension>
            value={dimension}
            onChange={(value) => set({ dimension: value })}
            options={Object.values(WorkDistributionDimension).map((value) => ({
              value,
              label: DIMENSION_LABELS[value],
            }))}
          />
        </Space>
        {measure !== WorkDistributionMeasure.Count && (
          <Space>
            <Text type="secondary">Unestimated items</Text>
            <Segmented<UnestimatedHandling>
              value={settings.unestimated}
              onChange={(value) => set({ unestimated: value })}
              options={[
                { value: UnestimatedHandling.Exclude, label: 'Exclude' },
                {
                  value: UnestimatedHandling.TeamAverage,
                  label: 'Use team average',
                },
              ]}
            />
          </Space>
        )}
        {dimension === WorkDistributionDimension.StrategicTheme && (
          <Space>
            <Text type="secondary">Several themes</Text>
            <Segmented<ThemeCounting>
              value={settings.themeCounting}
              onChange={(value) => set({ themeCounting: value })}
              options={[
                { value: ThemeCounting.SplitEvenly, label: 'Split evenly' },
                {
                  value: ThemeCounting.CountFully,
                  label: 'Count fully in each',
                },
              ]}
            />
          </Space>
        )}
      </Flex>

      {error ? (
        <Alert
          type="error"
          showIcon
          title="The work distribution report could not be loaded."
          description={
            validationMessages(error).length > 0 && (
              <ul style={{ margin: 0, paddingInlineStart: 20 }}>
                {validationMessages(error).map((message) => (
                  <li key={message}>{message}</li>
                ))}
              </ul>
            )
          }
        />
      ) : (
        <>
          <Flex gap="middle" wrap>
            <SummaryCards
              distribution={distribution}
              measure={measure}
              loading={isLoading && !distribution}
            />
          </Flex>

          <Tabs
            activeKey={activeView}
            onChange={(key) => setView(key as WorkDistributionView)}
            items={[
              { key: 'breakdown', label: 'Breakdown' },
              ...(isTeamOfTeams ? [{ key: 'teams', label: 'By Team' }] : []),
              { key: 'time', label: 'Over Time' },
            ]}
          />
          <Spin spinning={isLoading && !!distribution}>{content}</Spin>
          {dimension === WorkDistributionDimension.StrategicTheme && (
            <Text type="secondary" style={{ fontSize: 12 }}>
              A project uses its own themes, or its program&apos;s when it has
              none.
              {settings.themeCounting === ThemeCounting.SplitEvenly &&
                ' Work on a project with several themes is split evenly between them.'}
            </Text>
          )}
          {measure === WorkDistributionMeasure.TeamEffort && (
            <Text type="secondary" style={{ fontSize: 12 }}>
              Each team&apos;s split is measured in its own sizing method —
              story points, effort, size, or items for teams that size by count,
              as each sized on the day the work was done — then combined by
              share of completed items, so the percentages never add estimates
              in different units. Story point totals are still plain sums across
              teams.
            </Text>
          )}
        </>
      )}
    </Flex>
  )
}

export interface WorkDistributionReportProps {
  teamType: 'team' | 'team-of-teams'
  teamCode: string
}

const defaultSettings = (): WorkDistributionSettings => ({
  range: [yesterday().subtract(89, 'day'), yesterday()],
  dimension: WorkDistributionDimension.Portfolio,
  measure: WorkDistributionMeasure.Count,
  unestimated: UnestimatedHandling.Exclude,
  themeCounting: ThemeCounting.SplitEvenly,
})

export const WorkDistributionReport: FC<WorkDistributionReportProps> = ({
  teamType,
  teamCode,
}) => {
  const [settings, setSettings] =
    useState<WorkDistributionSettings>(defaultSettings)

  const { data, isFetching, error } = useGetTeamWorkDistributionQuery({
    teamType,
    teamIdOrCode: teamCode,
    from: settings.range[0].format(DATE_FORMAT),
    to: settings.range[1].format(DATE_FORMAT),
    dimension: settings.dimension,
    measure: settings.measure,
    unestimated: settings.unestimated,
    themeCounting: settings.themeCounting,
  })

  return (
    <WorkDistributionReportView
      distribution={data}
      isLoading={isFetching}
      error={error}
      settings={settings}
      onSettingsChange={setSettings}
      isTeamOfTeams={teamType === 'team-of-teams'}
    />
  )
}

export default WorkDistributionReport
