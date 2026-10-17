'use client'

import { useState } from 'react'
import { type Dayjs } from 'dayjs'
import Link from 'next/link'
import {
  Alert,
  Button,
  DatePicker,
  Descriptions,
  Flex,
  Statistic,
  Tooltip,
  Typography,
} from 'antd'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import useAuth from '@/src/components/contexts/auth'
import {
  formatCalendarDate,
  isApiError,
  parseCalendarDate,
  toCalendarDate,
  calendarDateInZone,
  todayCalendarDate,
} from '@/src/utils'
import {
  CapacityTeamType,
  useGetTeamCapacityQuery,
  useGetTeamOfTeamsCapacityRollupQuery,
} from '@/src/store/features/organization/team-capacity-api'
import {
  useGetTeamMembersQuery,
  useGetTeamOfTeamsMembersQuery,
} from '@/src/store/features/organization/team-members-api'
import ChangeAllocationForm from '@/src/app/organizations/_components/allocations/change-allocation-form'
import MoveAllocationForm from '@/src/app/organizations/_components/allocations/move-allocation-form'
import InitializeAllocationsForm from './initialize-allocations-form'
import TeamCapacityRollup from './team-capacity-rollup'
import {
  cadencePeriodOf,
  CapacityCadence,
  CapacityRange,
  CapacityRow,
  capacityRows,
  formatPersonDays,
  monthOf,
  NO_ROLE_LABEL,
  roleLines,
  timeAwayLines,
} from './team-capacity-data'
import styles from './team-capacity.module.css'

const { RangePicker } = DatePicker
const { Title } = Typography

/** The longest range the capacity endpoint reads, in days. */
const MAX_RANGE_DAYS = 731

// Sorts an allocation with no end after every dated one.
const NO_END_SORT_VALUE = '9999-12-31'

// On a team of teams the section holds two grids, so neither can take the rest
// of the viewport as a lone grid does.
const DIRECT_GRID_HEIGHT = 320

interface Props {
  team: { id: string; name: string; isActive: boolean }
  teamType: CapacityTeamType
  /** A Kanban team's reporting cadence: the section opens on its current period, not the current month. */
  cadence?: CapacityCadence
  /**
   * The IANA zone the team's days are counted in, so the default range is the
   * team's current month or period, not the viewer's. The viewer's zone when absent.
   */
  timeZone?: string
}

/** Which form is open, and for which row of the grid. */
type OpenForm =
  | { kind: 'add' }
  | { kind: 'initialize' }
  | { kind: 'change'; row: CapacityRow }
  | { kind: 'move'; row: CapacityRow }

/**
 * A team's capacity over a date range: who is allocated to it and what that
 * comes to in person-days. It holds no plan of its own; every edit changes
 * the person's allocations. A team of teams also shows the teams beneath it,
 * read-only, and its totals include them.
 */
const TeamCapacity = ({ team, teamType, cadence, timeZone }: Props) => {
  const today = timeZone
    ? calendarDateInZone(new Date(), timeZone)
    : todayCalendarDate()
  const currentMonth = monthOf(today)
  const currentPeriod = cadence ? cadencePeriodOf(today, cadence) : undefined
  const [range, setRange] = useState<CapacityRange>(
    () => currentPeriod ?? currentMonth,
  )
  const [openForm, setOpenForm] = useState<OpenForm | null>(null)

  const { hasPermissionClaim } = useAuth()
  const canUpdateTeams = hasPermissionClaim('Permissions.Teams.Update')

  // `currentData`, not `data`: `data` keeps the previous range's result while
  // a new range loads or after it fails, and its rows would be shown, and
  // edited, under the new range.
  const {
    currentData: data,
    isFetching,
    error,
    refetch,
  } = useGetTeamCapacityQuery({ teamId: team.id, teamType, ...range })

  const isTeamOfTeams = teamType === 'TeamOfTeams'
  const {
    currentData: rollup,
    isFetching: rollupIsFetching,
    error: rollupError,
    refetch: refetchRollup,
  } = useGetTeamOfTeamsCapacityRollupQuery(
    { teamId: team.id, ...range },
    { skip: !isTeamOfTeams },
  )

  const { data: teamMembers } = useGetTeamMembersQuery(
    { teamId: team.id },
    { skip: teamType !== 'Team' },
  )
  const { data: teamOfTeamsMembers } = useGetTeamOfTeamsMembersQuery(
    { teamId: team.id },
    { skip: teamType !== 'TeamOfTeams' },
  )
  const members = teamType === 'Team' ? teamMembers : teamOfTeamsMembers
  const memberIds = new Set(members?.map((m) => m.employee.id))

  // The commands refuse new time on an inactive team, so it is read-only.
  const canEdit = canUpdateTeams && data?.canManage === true && team.isActive

  const rows = capacityRows(data?.people ?? [])
  // A team of teams' totals and roles cover the teams beneath it too.
  const totals = isTeamOfTeams
    ? {
        people: rollup?.people ?? 0,
        allocatedPersonDays: rollup?.allocatedPersonDays ?? 0,
        deliveryPersonDays: rollup?.deliveryPersonDays ?? 0,
        roles: rollup?.roles ?? [],
      }
    : {
        people: data?.people.length ?? 0,
        allocatedPersonDays: data?.allocatedPersonDays ?? 0,
        deliveryPersonDays: data?.deliveryPersonDays ?? 0,
        roles: data?.roles ?? [],
      }
  const lines = roleLines(totals.roles)
  const loadError = error ?? rollupError
  const closeForm = () => setOpenForm(null)

  const columns: ColumnDef<CapacityRow, any>[] = [
    {
      id: 'person',
      accessorFn: (row) => row.person.employee.name,
      header: 'Person',
      size: 200,
      cell: ({ row }) => (
        <Link
          href={`/organizations/employees/${row.original.person.employee.key}`}
        >
          {row.original.person.employee.name}
        </Link>
      ),
    },
    {
      id: 'role',
      accessorFn: (row) => row.role?.name ?? NO_ROLE_LABEL,
      header: 'Role',
      size: 150,
      meta: { filterEnableSet: true },
    },
    {
      id: 'delivery',
      accessorFn: (row) => (row.countsTowardDeliveryCapacity ? 'Yes' : 'No'),
      header: 'Delivery',
      size: 100,
      meta: { filterType: 'set' },
    },
    {
      id: 'percent',
      accessorKey: 'percent',
      header: 'Percent',
      size: 90,
      cell: ({ row }) => `${row.original.percent}%`,
    },
    {
      id: 'start',
      accessorKey: 'start',
      header: 'Start',
      size: 110,
      cell: ({ row }) => formatCalendarDate(row.original.start),
    },
    {
      id: 'end',
      accessorFn: (row) => row.end ?? NO_END_SORT_VALUE,
      header: 'End',
      size: 110,
      cell: ({ row }) =>
        row.original.end ? formatCalendarDate(row.original.end) : 'No end',
    },
    {
      id: 'personDays',
      accessorKey: 'personDays',
      header: 'Person-days',
      size: 110,
      cell: ({ row }) => formatPersonDays(row.original.personDays),
    },
    {
      id: 'elsewhere',
      accessorFn: (row) => row.person.elsewherePersonDays,
      header: 'Elsewhere',
      size: 110,
      cell: ({ row }) =>
        formatPersonDays(row.original.person.elsewherePersonDays),
    },
    {
      id: 'away',
      accessorFn: (row) => row.person.awayPersonDays,
      header: 'Away',
      size: 90,
      cell: ({ row }) => {
        const lines = timeAwayLines(
          row.original.person.timeAway,
          row.original.person.workingDayHours,
        )
        const away = formatPersonDays(row.original.person.awayPersonDays)
        return lines.length > 0 ? (
          <Tooltip
            title={lines.map((line) => (
              <div key={line}>{line}</div>
            ))}
          >
            <span>{away}</span>
          </Tooltip>
        ) : (
          away
        )
      },
    },
    ...(canEdit
      ? [
          {
            id: 'actions',
            header: '',
            size: 170,
            enableSorting: false,
            enableColumnFilter: false,
            cell: ({ row }) => (
              <Flex gap="small">
                <Button
                  type="link"
                  size="small"
                  onClick={() =>
                    setOpenForm({ kind: 'change', row: row.original })
                  }
                >
                  Change
                </Button>
                {row.original.person.elsewhereAllocations.some(
                  (a) => a.canManage,
                ) && (
                  <Button
                    type="link"
                    size="small"
                    onClick={() =>
                      setOpenForm({ kind: 'move', row: row.original })
                    }
                  >
                    Rebalance
                  </Button>
                )}
              </Flex>
            ),
          } satisfies ColumnDef<CapacityRow, any>,
        ]
      : []),
  ]

  return (
    <div>
      <Flex gap="small" align="center" className={styles.controls}>
        <RangePicker
          allowClear={false}
          aria-label="Capacity date range"
          value={[parseCalendarDate(range.from), parseCalendarDate(range.to)]}
          presets={[
            ...(currentPeriod
              ? [
                  {
                    label: 'This period',
                    value: [
                      parseCalendarDate(currentPeriod.from),
                      parseCalendarDate(currentPeriod.to),
                    ] as [Dayjs, Dayjs],
                  },
                ]
              : []),
            {
              label: 'This month',
              value: [
                parseCalendarDate(currentMonth.from),
                parseCalendarDate(currentMonth.to),
              ] as [Dayjs, Dayjs],
            },
          ]}
          disabledDate={(current, { from }) =>
            !!from && Math.abs(current.diff(from, 'day')) >= MAX_RANGE_DAYS
          }
          onChange={(dates) => {
            const [from, to] = dates ?? []
            if (from && to)
              setRange({ from: toCalendarDate(from), to: toCalendarDate(to) })
          }}
        />
      </Flex>

      {loadError ? (
        <Alert
          type="error"
          showIcon
          className={styles.notice}
          title="The team's capacity could not be loaded for this date range."
          description={isApiError(loadError) ? loadError.detail : undefined}
        />
      ) : (
        <>
          <Flex gap="large" wrap className={styles.summary}>
            <Statistic
              title={
                isTeamOfTeams
                  ? 'Working days (direct allocations)'
                  : 'Working days'
              }
              value={data?.workingDays ?? 0}
            />
            <Statistic title="People" value={totals.people} />
            <Statistic
              title="Allocated person-days"
              value={formatPersonDays(totals.allocatedPersonDays)}
            />
            <Statistic
              title="Delivery person-days"
              value={formatPersonDays(totals.deliveryPersonDays)}
            />
            {!isTeamOfTeams && (
              <Statistic
                title="Time away person-days"
                value={formatPersonDays(data?.awayPersonDays ?? 0)}
              />
            )}
          </Flex>

          {lines.length > 0 && (
            <Descriptions
              title="By role"
              size="small"
              column={1}
              bordered
              className={styles.roles}
              items={lines.map((line) => ({
                key: line.key,
                label: line.label,
                children: formatPersonDays(line.personDays),
              }))}
            />
          )}

          {canEdit && !isFetching && rows.length === 0 && (
            <Alert
              type="info"
              showIcon
              className={styles.notice}
              title="No one is allocated to this team in this date range."
              description="Start from the team's members: each is proposed at what is left of their time, and nothing is saved until you confirm."
              action={
                <Button
                  size="small"
                  onClick={() => setOpenForm({ kind: 'initialize' })}
                >
                  Initialize
                </Button>
              }
            />
          )}
        </>
      )}

      {isTeamOfTeams && <Title level={5}>Direct allocations</Title>}

      <WaydGrid
        columns={columns}
        data={rows}
        isLoading={isFetching}
        height={isTeamOfTeams ? DIRECT_GRID_HEIGHT : undefined}
        onRefresh={() => {
          refetch()
        }}
        leftSlot={
          canEdit ? (
            <Button size="small" onClick={() => setOpenForm({ kind: 'add' })}>
              Allocate Person
            </Button>
          ) : undefined
        }
        emptyMessage="No one is allocated in this date range."
        persistStateKey="team-capacity"
        csvFileName="team-capacity"
      />

      {/* Not on a failed load: with no teams to show, the grid would say there were none. */}
      {isTeamOfTeams && !rollupError && (
        <>
          <Title level={5} className={styles.rollupTitle}>
            Teams
          </Title>
          <TeamCapacityRollup
            nodes={rollup?.children ?? []}
            range={range}
            isLoading={rollupIsFetching}
            onRefresh={() => {
              refetchRollup()
            }}
          />
        </>
      )}

      {openForm?.kind === 'add' && (
        <ChangeAllocationForm
          team={team}
          initialDates={{ start: range.from, end: range.to }}
          // A member's time on the team runs on; a non-member's is for the
          // range. Until the members have loaded no one can be told apart, so
          // the end is left as the form opened it.
          endDateFor={
            members
              ? (employeeId) =>
                  memberIds.has(employeeId) ? undefined : range.to
              : undefined
          }
          onFormComplete={closeForm}
          onFormCancel={closeForm}
        />
      )}

      {openForm?.kind === 'change' && (
        <ChangeAllocationForm
          employeeId={openForm.row.person.employee.id}
          employeeName={openForm.row.person.employee.name}
          allocation={{
            teamId: team.id,
            teamName: team.name,
            roleId: openForm.row.role?.id,
            roleName: openForm.row.role?.name,
            start: openForm.row.start,
            end: openForm.row.end,
            percent: openForm.row.percent,
          }}
          onFormComplete={closeForm}
          onFormCancel={closeForm}
        />
      )}

      {openForm?.kind === 'move' && (
        <MoveAllocationForm
          employeeId={openForm.row.person.employee.id}
          allocations={openForm.row.person.elsewhereAllocations}
          target={{
            teamId: team.id,
            roleId: openForm.row.role?.id,
            start: range.from,
            end: range.to,
          }}
          onFormComplete={closeForm}
          onFormCancel={closeForm}
        />
      )}

      {openForm?.kind === 'initialize' && (
        <InitializeAllocationsForm
          teamId={team.id}
          teamType={teamType}
          from={range.from}
          onFormComplete={closeForm}
          onFormCancel={closeForm}
        />
      )}
    </div>
  )
}

export default TeamCapacity
