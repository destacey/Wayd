'use client'

import { DependencyWheelExplorer } from '@/src/components/common/dependency-wheel'
import { DependencyPartnersExplorer } from '@/src/components/common/work/dependency-partners'
import {
  DependencyDto,
  TeamDependencyAnalysisDto,
} from '@/src/services/wayd-api'
import { useGetTeamDependencyAnalysisQuery } from '@/src/store/features/organizations/team-api'
import {
  parseCalendarDate,
  toCalendarDate,
  todayInZone,
} from '@/src/utils/calendar-date'
import { isApiError } from '@/src/utils/problem-details'
import { InfoCircleOutlined } from '@ant-design/icons'
import type { TimeRangePickerProps } from 'antd'
import {
  Alert,
  Button,
  Checkbox,
  DatePicker,
  Flex,
  Space,
  Spin,
  Tabs,
  Typography,
} from 'antd'
import { Dayjs } from 'dayjs'
import { FC, useState } from 'react'
import { StatusCategoryId } from '../../../types'
import { METRIC_CARD_FLEX, MetricCard } from '../../metrics'
import WaydEmpty from '../../wayd-empty'
import WaydTooltip from '../../wayd-tooltip'
import DependencyAnalysisOverTime from './dependency-analysis-over-time'
import DependencyAnalysisPairs from './dependency-analysis-pairs'

const { Title, Text } = Typography
const { RangePicker } = DatePicker

type DependencyAnalysisView = 'where' | 'pairs' | 'time'

/** Two years with room for a leap day, both ends inclusive; the API rejects longer ranges. */
const MAX_RANGE_DAYS = 731

/** Today in the team's time zone, as a picker value; in UTC without one, as the API counts it. */
const todayFor = (timeZone: string | undefined) =>
  parseCalendarDate(todayInZone(timeZone ?? 'UTC'))

const rangePresets = (
  timeZone: string | undefined,
): TimeRangePickerProps['presets'] => {
  const today = () => todayFor(timeZone)
  return [
    {
      label: 'Last 30 days',
      value: () => [today().subtract(29, 'day'), today()],
    },
    {
      label: 'Last 90 days',
      value: () => [today().subtract(89, 'day'), today()],
    },
    {
      label: 'Last 6 months',
      value: () => [today().subtract(6, 'month').add(1, 'day'), today()],
    },
    {
      label: 'Last 12 months',
      value: () => [today().subtract(12, 'month').add(1, 'day'), today()],
    },
    {
      label: 'Last 2 years',
      value: () => [today().subtract(2, 'year').add(1, 'day'), today()],
    },
  ]
}

/**
 * A dependency whose predecessor was removed never constrained anything, or no longer can: data to clean up
 * by deleting the link.
 */
const hasRemovedPredecessor = (dependency: DependencyDto) =>
  dependency.source.statusCategory.id === StatusCategoryId.Removed

/** The API's validation messages, when the request was rejected as invalid. */
const validationMessages = (error: unknown): string[] =>
  isApiError(error) && error.errors ? Object.values(error.errors).flat() : []

export interface DependencyAnalysisReportViewProps {
  analysis?: TeamDependencyAnalysisDto
  isLoading: boolean
  error?: unknown
  refetch: () => void
  range: [Dayjs, Dayjs]
  onRangeChange: (range: [Dayjs, Dayjs]) => void
  /** A team of teams covers every team beneath it and draws a wheel; a team draws its partners. */
  isTeamOfTeams: boolean
  /** The team's time zone, which decides what today is; UTC when absent. */
  timeZone?: string
}

export const DependencyAnalysisReportView: FC<
  DependencyAnalysisReportViewProps
> = ({
  analysis,
  isLoading,
  error,
  refetch,
  range,
  onRangeChange,
  isTeamOfTeams,
  timeZone,
}) => {
  const [view, setView] = useState<DependencyAnalysisView>('where')
  const [onlyRemoved, setOnlyRemoved] = useState(false)
  const summary = analysis?.summary
  // The checkbox is hidden when there are none, so the filter must not apply then either.
  const filterRemoved = onlyRemoved && !!summary?.predecessorRemoved
  const shown = filterRemoved
    ? (analysis?.dependencies.filter(hasRemovedPredecessor) ?? [])
    : (analysis?.dependencies ?? [])
  const explorerKey = `${toCalendarDate(range[0])}:${toCalendarDate(range[1])}:${filterRemoved}`
  const gridKey = isTeamOfTeams
    ? 'team-of-teams-dependency-analysis'
    : 'team-dependency-analysis'
  const emptyMessage = 'No dependencies crossed team lines in this date range.'

  const card = (title: string, value: number | undefined, tooltip?: string) => (
    <MetricCard
      key={title}
      title={title}
      value={value ?? ''}
      tooltip={tooltip}
      // While a new range loads, the cards would otherwise still show the old range's totals.
      loading={isLoading}
      cardStyle={METRIC_CARD_FLEX}
    />
  )

  const content = (() => {
    if (!analysis) return <Spin />
    if (analysis.summary.dependencies === 0)
      return <WaydEmpty message={emptyMessage} />

    switch (view) {
      case 'pairs':
        return (
          <DependencyAnalysisPairs
            pairs={analysis.pairs}
            markOutside={isTeamOfTeams}
          />
        )
      case 'time':
        return <DependencyAnalysisOverTime periods={analysis.periods} />
      default:
        // A new range or filter is a new set of dependencies, so a pick on the old one does not carry over.
        // The outer Spin covers a refetch, so the explorers never swap their content for a skeleton.
        return isTeamOfTeams ? (
          <DependencyWheelExplorer
            key={explorerKey}
            dependencies={shown}
            isLoading={false}
            refetch={refetch}
            gridKey={gridKey}
            exportFileName={`team-of-teams-${analysis.team.key}-dependency-analysis`}
            emptyMessage={emptyMessage}
          />
        ) : (
          <DependencyPartnersExplorer
            key={explorerKey}
            teamId={analysis.team.id}
            dependencies={shown}
            isLoading={false}
            refetch={refetch}
            gridKey={gridKey}
            emptyMessage={emptyMessage}
          />
        )
    }
  })()

  return (
    <Flex vertical gap="middle">
      <Space>
        <Title level={4} style={{ margin: 0 }}>
          Dependency Analysis
        </Title>
        <WaydTooltip
          title={
            isTeamOfTeams
              ? 'Where the dependencies of the teams beneath this team of teams ran over the date range: the teams they ran between, inside the team of teams and out of it, and how that changed. Dependencies within one team are left out.'
              : 'Where the dependencies between this team and other teams ran over the date range, and how that changed.'
          }
          helpCursor
        >
          <InfoCircleOutlined />
        </WaydTooltip>
      </Space>

      <Space>
        <Text type="secondary">Date range</Text>
        <RangePicker
          aria-label="Date range"
          allowClear={false}
          value={range}
          presets={rangePresets(timeZone)}
          // Once one end is picked, the other can be at most two years away, as the API allows.
          disabledDate={(date, { from }) =>
            date.isAfter(todayFor(timeZone), 'day') ||
            (!!from && Math.abs(date.diff(from, 'day')) + 1 > MAX_RANGE_DAYS)
          }
          onChange={(value) => {
            if (!value?.[0] || !value[1]) return
            // A new range is a new set to clean up, so the filter starts off again.
            setOnlyRemoved(false)
            onRangeChange([value[0], value[1]])
          }}
        />
      </Space>

      {error ? (
        <Alert
          type="error"
          showIcon
          title="The dependency analysis could not be loaded."
          action={
            <Button size="small" onClick={refetch}>
              Retry
            </Button>
          }
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
            {card(
              'Dependencies',
              summary?.dependencies,
              'Dependencies open at some point in the range: created before it ended, and not closed before it began.',
            )}
            {card('Created', summary?.created, 'Created in the range.')}
            {card(
              'Closed',
              summary?.closed,
              'Closed in the range: the predecessor was done, or was removed with the successor done or removed too.',
            )}
            {card(
              'Open at End',
              summary?.openAtEnd,
              'Still open on the last day of the range.',
            )}
            {card(
              'Predecessor Removed',
              summary?.predecessorRemoved,
              'Dependencies whose predecessor was removed: the link never held anything up, or no longer can. They still count everywhere else until the link is deleted.',
            )}
            {card(
              'Teams Involved',
              summary?.teamsInvolved,
              'Teams owning either end of a dependency.',
            )}
            {card(
              isTeamOfTeams ? 'Teams Outside' : 'Partner Teams',
              summary?.teamsOutside,
              isTeamOfTeams
                ? 'Teams outside this team of teams that its teams share dependencies with.'
                : 'Other teams this team shares dependencies with.',
            )}
          </Flex>

          <Tabs
            activeKey={view}
            onChange={(key) => setView(key as DependencyAnalysisView)}
            items={[
              { key: 'where', label: 'Where' },
              { key: 'pairs', label: 'Team Pairs' },
              { key: 'time', label: 'Over Time' },
            ]}
          />
          {view === 'where' && !!summary?.predecessorRemoved && (
            <Checkbox
              checked={onlyRemoved}
              onChange={(e) => setOnlyRemoved(e.target.checked)}
            >
              Only dependencies whose predecessor was removed
            </Checkbox>
          )}
          <Spin spinning={isLoading && !!analysis}>{content}</Spin>
          <Text type="secondary" style={{ fontSize: 12 }}>
            {analysis &&
              `The date range and periods are whole days in the ${isTeamOfTeams ? "team of teams'" : "team's"} time zone, ${analysis.timeZone} at the end of the range. `}
            Teams are the items&apos; current teams. State and health are as
            they are today. Removed links are left out.
          </Text>
        </>
      )}
    </Flex>
  )
}

export interface DependencyAnalysisReportProps {
  teamType: 'team' | 'team-of-teams'
  teamCode: string
  /** The time zone of the team's, or the team of teams', current operating model. */
  timeZone?: string
}

const defaultRange = (timeZone: string | undefined): [Dayjs, Dayjs] => [
  todayFor(timeZone).subtract(89, 'day'),
  todayFor(timeZone),
]

export const DependencyAnalysisReport: FC<DependencyAnalysisReportProps> = ({
  teamType,
  teamCode,
  timeZone,
}) => {
  const [range, setRange] = useState<[Dayjs, Dayjs]>(() =>
    defaultRange(timeZone),
  )

  const { data, isFetching, error, refetch } =
    useGetTeamDependencyAnalysisQuery({
      teamType,
      teamIdOrCode: teamCode,
      from: toCalendarDate(range[0]),
      to: toCalendarDate(range[1]),
    })

  return (
    <DependencyAnalysisReportView
      analysis={data}
      isLoading={isFetching}
      error={error}
      refetch={refetch}
      range={range}
      onRangeChange={setRange}
      isTeamOfTeams={teamType === 'team-of-teams'}
      timeZone={timeZone}
    />
  )
}

export default DependencyAnalysisReport
