'use client'

import Link from 'next/link'
import { useState } from 'react'
import {
  CaretDownOutlined,
  CaretRightOutlined,
  InfoCircleOutlined,
} from '@ant-design/icons'
import {
  Alert,
  Button,
  DatePicker,
  Flex,
  Segmented,
  Select,
  Space,
  Tabs,
  Typography,
} from 'antd'
import { useLocalStorageState } from '@/src/hooks'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import treeGridStyles from '@/src/components/common/wayd-grid/wayd-grid.module.css'
import { METRIC_CARD_FLEX, MetricCard } from '@/src/components/common/metrics'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import {
  CapacityReportInterval,
  CapacityTeamType,
  TeamCapacityReportRoleDto,
  useGetTeamCapacityReportQuery,
} from '@/src/store/features/organization/team-capacity-api'
import { useGetTeamSprintsQuery } from '@/src/store/features/organizations/team-api'
import {
  useGetPlanningIntervalIterationsQuery,
  useGetPlanningIntervalsQuery,
} from '@/src/store/features/planning/planning-interval-api'
import {
  calendarDateInZone,
  calendarDaysBetween,
  formatCalendarDate,
  parseCalendarDate,
  todayCalendarDate,
  toCalendarDate,
} from '@/src/utils'
import { isApiError } from '@/src/utils/problem-details'
import {
  CapacityCadence,
  CapacityRange,
  cadencePeriodOf,
  monthOf,
} from '../team-capacity/team-capacity-data'
import {
  CapacityReportGrouping,
  CapacityReportRow,
  CapacityReportUnit,
  formatCapacity,
  grandTotal,
  previousMonthOf,
  quarterOf,
  rowsByPerson,
  rowsByTeam,
} from './capacity-report-data'

const { Title, Text } = Typography
const { RangePicker } = DatePicker

const GRID_HEIGHT = 520

/** Two years, both ends inclusive, as the API allows. */
const MAX_RANGE_DAYS = 731

type IntervalChoice = 'none' | CapacityReportInterval

/** The API's validation messages, when the request was rejected as invalid. */
const validationMessages = (error: unknown): string[] =>
  isApiError(error) && error.errors ? Object.values(error.errors).flat() : []

export interface CapacityReportProps {
  teamId: string
  teamType: CapacityTeamType
  /** The team's time zone, which decides what today is; the browser's when absent. */
  timeZone?: string
  /** A Kanban team's reporting cadence, for the cadence period shortcut. */
  cadence?: CapacityCadence
  /** Whether the team has sprints to offer as periods. */
  hasSprints?: boolean
}

/**
 * Who a team, or a team of teams and everything beneath it, had over a date
 * range: the time allocated, what holidays and time away took from it, and
 * what was left, grouped by team or by person.
 */
const CapacityReport = ({
  teamId,
  teamType,
  timeZone,
  cadence,
  hasSprints = false,
}: CapacityReportProps) => {
  const today = timeZone
    ? calendarDateInZone(new Date(), timeZone)
    : todayCalendarDate()

  const [range, setRange] = useState<CapacityRange>(() => monthOf(today))
  const [grouping, setGrouping] = useState<CapacityReportGrouping>('team')
  const [columnInterval, setColumnInterval] = useState<IntervalChoice>('none')
  const [unit, setUnit] = useLocalStorageState<CapacityReportUnit>(
    'capacity-report-unit',
    'days',
  )
  const [planningIntervalKey, setPlanningIntervalKey] = useState<number>()

  // `data` keeps the last range's report while a new one loads, which the grid
  // shows under its loading overlay; the totals read only the current one.
  const { data, currentData, isFetching, error, refetch } =
    useGetTeamCapacityReportQuery({
      teamId,
      teamType,
      from: range.from,
      to: range.to,
      interval: columnInterval === 'none' ? undefined : columnInterval,
    })

  const { data: sprints } = useGetTeamSprintsQuery(teamId, {
    skip: !hasSprints,
  })
  const { data: planningIntervals } = useGetPlanningIntervalsQuery()
  const { data: iterations } = useGetPlanningIntervalIterationsQuery(
    planningIntervalKey!,
    { skip: planningIntervalKey === undefined },
  )

  // A sprint or iteration stays shown as picked while the range is still its dates.
  const coversRange = (period: { start: string; end: string }) =>
    period.start === range.from && period.end === range.to
  const selectedSprint = sprints?.find(coversRange)
  const selectedIteration = iterations?.find(coversRange)

  const hours = data?.workingDayHours ?? 8
  const format = (personDays: number | undefined) =>
    personDays === undefined ? '' : formatCapacity(personDays, unit, hours)
  const unitLabel = unit === 'hours' ? 'hours' : 'person-days'

  const rows = data
    ? grouping === 'team'
      ? rowsByTeam(data)
      : rowsByPerson(data)
    : []
  const total = currentData?.total

  // A planning interval is a shortcut like the others: a range picked any
  // other way leaves it.
  const pickRange = (picked: CapacityRange) => {
    setPlanningIntervalKey(undefined)
    setRange(picked)
  }

  const currentMonth = monthOf(today)
  const previousMonth = previousMonthOf(today)
  const currentQuarter = quarterOf(today)
  const previousQuarter = quarterOf(today, -1)
  const currentPeriod = cadence ? cadencePeriodOf(today, cadence) : undefined
  const preset = (label: string, r: CapacityRange) => ({
    label,
    value: [parseCalendarDate(r.from), parseCalendarDate(r.to)] as [
      ReturnType<typeof parseCalendarDate>,
      ReturnType<typeof parseCalendarDate>,
    ],
  })

  const measureColumn = (
    id: string,
    header: string,
    value: (row: CapacityReportRow) => number | undefined,
    tooltip?: string,
  ): ColumnDef<CapacityReportRow, any> => ({
    id,
    accessorFn: (row) => value(row) ?? null,
    header: tooltip
      ? () => (
          <WaydTooltip title={tooltip}>
            <span>{header}</span>
          </WaydTooltip>
        )
      : header,
    size: 110,
    cell: ({ row }) => format(value(row.original)),
    meta: {
      exportHeader: header,
      exportFormatter: (v) => (typeof v === 'number' ? format(v) : ''),
    },
  })

  const columns: ColumnDef<CapacityReportRow, any>[] = [
    {
      id: 'name',
      accessorKey: 'name',
      header: 'Name',
      size: 300,
      // A spreadsheet has no tree, so each row carries where it sits.
      meta: {
        exportHeader: 'Path',
        exportFormatter: (_, row: CapacityReportRow) => row.path,
      },
      // In tree mode the grid renders rows flat and leaves depth to the cell.
      cell: ({ row }) => (
        <Flex align="center" gap={0} className={treeGridStyles.nameCell}>
          {Array.from({ length: row.depth }).map((_, index) => (
            <span key={index} className={treeGridStyles.indentSpacer} />
          ))}
          {row.getCanExpand() ? (
            <Button
              type="text"
              size="small"
              aria-label={
                row.getIsExpanded()
                  ? `Collapse ${row.original.name}`
                  : `Expand ${row.original.name}`
              }
              icon={
                row.getIsExpanded() ? (
                  <CaretDownOutlined />
                ) : (
                  <CaretRightOutlined />
                )
              }
              onClick={row.getToggleExpandedHandler()}
              className={treeGridStyles.expanderBtn}
            />
          ) : (
            <span className={treeGridStyles.indentSpacer} />
          )}
          {row.original.href ? (
            <Link href={row.original.href}>{row.original.name}</Link>
          ) : (
            <Text type="secondary">{row.original.name}</Text>
          )}
        </Flex>
      ),
    },
    {
      id: 'type',
      accessorKey: 'type',
      header: 'Type',
      size: 120,
      meta: { filterType: 'set' },
    },
    {
      id: 'roles',
      accessorFn: (row) => row.roles ?? '',
      header: 'Roles',
      size: 180,
    },
    measureColumn(
      'allocated',
      'Allocated',
      (r) => r.total.allocatedPersonDays,
      'Time allocated over the working days, before holidays and time away.',
    ),
    measureColumn(
      'holidays',
      'Holidays',
      (r) => r.total.holidayPersonDays,
      'Allocated time that fell on holidays in the calendar each person takes.',
    ),
    measureColumn(
      'timeAway',
      'Time Away',
      (r) => r.total.timeAwayPersonDays,
      'Allocated time lost to time away. Why someone was away is never shown here.',
    ),
    measureColumn(
      'available',
      'Available',
      (r) => r.total.availablePersonDays,
      'What is left after holidays and time away.',
    ),
    measureColumn(
      'delivery',
      'Delivery',
      (r) => r.total.deliveryPersonDays,
      'Available time in roles that count toward delivery capacity.',
    ),
    measureColumn(
      'nonDelivery',
      'Non-Delivery',
      (r) => r.total.availablePersonDays - r.total.deliveryPersonDays,
      'Available time in roles that do not count toward delivery capacity.',
    ),
    ...(grouping === 'person'
      ? [
          measureColumn(
            'outside',
            'Outside',
            (r) => r.outsidePersonDays,
            "Each person's time allocated to teams outside this report, over the default working week.",
          ),
          measureColumn(
            'unallocated',
            'Unallocated',
            (r) => r.unallocatedPersonDays,
            "Each person's time allocated to no team, over the default working week.",
          ),
        ]
      : []),
    ...(data?.buckets ?? []).map((bucket, index) =>
      measureColumn(
        `bucket-${bucket.start}`,
        bucket.start === bucket.end
          ? formatCalendarDate(bucket.start)
          : `${formatCalendarDate(bucket.start)} – ${formatCalendarDate(bucket.end)}`,
        (r) => r.buckets[index]?.availablePersonDays,
      ),
    ),
  ]

  const roleColumns: ColumnDef<TeamCapacityReportRoleDto, any>[] = [
    {
      id: 'role',
      accessorFn: (r) => r.role?.name ?? 'No role',
      header: 'Role',
      size: 220,
    },
    {
      id: 'counts',
      accessorFn: (r) => (r.countsTowardDeliveryCapacity ? 'Yes' : 'No'),
      header: 'Delivery Capacity',
      size: 150,
      meta: { filterType: 'set' },
    },
    ...(
      [
        ['allocated', 'Allocated', 'allocatedPersonDays'],
        ['holidays', 'Holidays', 'holidayPersonDays'],
        ['timeAway', 'Time Away', 'timeAwayPersonDays'],
        ['available', 'Available', 'availablePersonDays'],
      ] as const
    ).map(([id, header, key]): ColumnDef<TeamCapacityReportRoleDto, any> => ({
      id,
      accessorFn: (r) => r.total[key],
      header,
      size: 120,
      cell: ({ getValue }) => format(getValue()),
      meta: {
        exportFormatter: (v) => (typeof v === 'number' ? format(v) : ''),
      },
    })),
  ]

  const card = (title: string, value: number | undefined, tooltip?: string) => (
    <MetricCard
      key={title}
      title={title}
      value={value === undefined ? '' : format(value)}
      secondaryValue={value === undefined ? undefined : unitLabel}
      tooltip={tooltip}
      loading={isFetching && !currentData}
      cardStyle={METRIC_CARD_FLEX}
    />
  )

  return (
    <Flex vertical gap="middle">
      <Space>
        <Title level={4} style={{ margin: 0 }}>
          Capacity Report
        </Title>
        <WaydTooltip
          title={
            teamType === 'TeamOfTeams'
              ? 'Who this team of teams and every team beneath it had over the range, each team counted only for the days it was in the hierarchy. An allocation counts once, on the team it was made to.'
              : 'Who this team had over the range, from the time people were allocated to it.'
          }
          helpCursor
        >
          <InfoCircleOutlined />
        </WaydTooltip>
      </Space>

      <Flex gap="middle" wrap align="center">
        <RangePicker
          allowClear={false}
          value={[parseCalendarDate(range.from), parseCalendarDate(range.to)]}
          presets={[
            ...(currentPeriod ? [preset('This period', currentPeriod)] : []),
            preset('This month', currentMonth),
            preset('Last month', previousMonth),
            preset('This quarter', currentQuarter),
            preset('Last quarter', previousQuarter),
          ]}
          disabledDate={(date, { from }) =>
            !!from &&
            Math.abs(
              calendarDaysBetween(toCalendarDate(from), toCalendarDate(date)),
            ) +
              1 >
              MAX_RANGE_DAYS
          }
          onChange={(value) => {
            const [from, to] = value ?? []
            if (from && to)
              pickRange({ from: toCalendarDate(from), to: toCalendarDate(to) })
          }}
        />
        {hasSprints && (
          <Select
            aria-label="Sprint"
            placeholder="Sprint"
            style={{ width: 200 }}
            showSearch={{ optionFilterProp: 'label' }}
            value={selectedSprint?.id ?? null}
            options={[...(sprints ?? [])]
              .sort((a, b) => b.start.localeCompare(a.start))
              .map((s) => ({ value: s.id, label: s.name }))}
            onChange={(id) => {
              const sprint = sprints?.find((s) => s.id === id)
              if (sprint) pickRange({ from: sprint.start, to: sprint.end })
            }}
          />
        )}
        <Select
          aria-label="Planning interval"
          placeholder="Planning interval"
          style={{ width: 200 }}
          showSearch={{ optionFilterProp: 'label' }}
          value={planningIntervalKey}
          options={[...(planningIntervals ?? [])]
            .sort((a, b) => b.start.localeCompare(a.start))
            .map((pi) => ({ value: pi.key, label: pi.name }))}
          onChange={(key) => {
            const pi = planningIntervals?.find((p) => p.key === key)
            setPlanningIntervalKey(key)
            if (pi) setRange({ from: pi.start, to: pi.end })
          }}
        />
        {planningIntervalKey !== undefined && (
          <Select
            aria-label="Iteration"
            placeholder="Iteration"
            style={{ width: 180 }}
            value={selectedIteration?.id ?? null}
            options={(iterations ?? []).map((i) => ({
              value: i.id,
              label: i.name,
            }))}
            onChange={(id) => {
              const iteration = iterations?.find((i) => i.id === id)
              if (iteration)
                setRange({ from: iteration.start, to: iteration.end })
            }}
          />
        )}
      </Flex>

      <Flex gap="middle" wrap align="center">
        <Space>
          <Text type="secondary">Group by</Text>
          <Segmented<CapacityReportGrouping>
            value={grouping}
            onChange={setGrouping}
            options={[
              { value: 'team', label: 'Team' },
              { value: 'person', label: 'Person' },
            ]}
          />
        </Space>
        <Space>
          <Text type="secondary">Columns</Text>
          <Segmented<IntervalChoice>
            value={columnInterval}
            onChange={setColumnInterval}
            options={[
              { value: 'none', label: 'Totals' },
              { value: CapacityReportInterval.Week, label: 'By week' },
              { value: CapacityReportInterval.Month, label: 'By month' },
            ]}
          />
        </Space>
        <Space>
          <Text type="secondary">Show</Text>
          <Segmented<CapacityReportUnit>
            value={unit}
            onChange={setUnit}
            options={[
              { value: 'days', label: 'Person-days' },
              { value: 'hours', label: 'Hours' },
            ]}
          />
        </Space>
      </Flex>

      {error ? (
        <Alert
          type="error"
          showIcon
          title="The capacity report could not be loaded."
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
              'Allocated',
              total?.allocatedPersonDays,
              'Time allocated over the working days, before holidays and time away.',
            )}
            {card('Lost to Holidays', total?.holidayPersonDays)}
            {card('Lost to Time Away', total?.timeAwayPersonDays)}
            {card('Available', total?.availablePersonDays)}
            {card(
              'Delivery',
              total?.deliveryPersonDays,
              'Available time in roles that count toward delivery capacity.',
            )}
          </Flex>

          <Tabs
            items={[
              {
                key: 'grid',
                label: grouping === 'team' ? 'By Team' : 'By Person',
                children: (
                  <Flex vertical gap="small">
                    <WaydGrid<CapacityReportRow>
                      columns={columns}
                      data={rows}
                      getSubRows={(row) => row.children}
                      getRowId={(row) => row.id}
                      // Teams open to their people; people closed to their teams.
                      initialExpanded={grouping === 'team'}
                      isLoading={isFetching}
                      onRefresh={refetch}
                      height={GRID_HEIGHT}
                      emptyMessage="No one was allocated in this date range."
                      csvFileName={`capacity-report-by-${grouping}`}
                    />
                    {currentData && rows.length > 0 && (
                      <Text type="secondary" style={{ fontSize: 12 }}>
                        Total available:{' '}
                        {format(grandTotal(rows).availablePersonDays)}{' '}
                        {unitLabel}.
                        {columnInterval !== 'none' &&
                          ' Week and month columns show available time.'}
                      </Text>
                    )}
                  </Flex>
                ),
              },
              {
                key: 'roles',
                label: 'By Role',
                children: (
                  <WaydGrid<TeamCapacityReportRoleDto>
                    columns={roleColumns}
                    data={[...(data?.roles ?? [])]}
                    isLoading={isFetching}
                    onRefresh={refetch}
                    height={GRID_HEIGHT}
                    emptyMessage="No one was allocated in this date range."
                    csvFileName="capacity-report-by-role"
                  />
                ),
              },
            ]}
          />
        </>
      )}
    </Flex>
  )
}

export default CapacityReport
