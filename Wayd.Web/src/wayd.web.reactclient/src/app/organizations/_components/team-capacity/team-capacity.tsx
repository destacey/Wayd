'use client'

import { useState } from 'react'
import Link from 'next/link'
import { Alert, Button, DatePicker, Descriptions, Flex, Statistic } from 'antd'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import useAuth from '@/src/components/contexts/auth'
import {
  formatCalendarDate,
  isApiError,
  parseCalendarDate,
  toCalendarDate,
  todayCalendarDate,
} from '@/src/utils'
import {
  CapacityTeamType,
  useGetTeamCapacityQuery,
} from '@/src/store/features/organization/team-capacity-api'
import {
  useGetTeamMembersQuery,
  useGetTeamOfTeamsMembersQuery,
} from '@/src/store/features/organization/team-members-api'
import ChangeAllocationForm from '@/src/app/organizations/_components/allocations/change-allocation-form'
import MoveAllocationForm from '@/src/app/organizations/_components/allocations/move-allocation-form'
import InitializeAllocationsForm from './initialize-allocations-form'
import {
  CapacityRange,
  CapacityRow,
  capacityRows,
  clipToRange,
  formatPersonDays,
  monthOf,
  NO_ROLE_LABEL,
  roleLines,
} from './team-capacity-data'
import styles from './team-capacity.module.css'

const { RangePicker } = DatePicker

/** The longest range the capacity endpoint reads, in days. */
const MAX_RANGE_DAYS = 731

// Sorts an allocation with no end after every dated one.
const NO_END_SORT_VALUE = '9999-12-31'

interface Props {
  team: { id: string; name: string; isActive: boolean }
  teamType: CapacityTeamType
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
 * the person's allocations.
 */
const TeamCapacity = ({ team, teamType }: Props) => {
  const [range, setRange] = useState<CapacityRange>(() =>
    monthOf(todayCalendarDate()),
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
  const lines = roleLines(data?.roles ?? [])
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

      {error ? (
        <Alert
          type="error"
          showIcon
          className={styles.notice}
          title="The team's capacity could not be loaded for this date range."
          description={isApiError(error) ? error.detail : undefined}
        />
      ) : (
        <>
          <Flex gap="large" wrap className={styles.summary}>
            <Statistic title="Working days" value={data?.workingDays ?? 0} />
            <Statistic title="People" value={data?.people.length ?? 0} />
            <Statistic
              title="Allocated person-days"
              value={formatPersonDays(data?.allocatedPersonDays ?? 0)}
            />
            <Statistic
              title="Delivery person-days"
              value={formatPersonDays(data?.deliveryPersonDays ?? 0)}
            />
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

      <WaydGrid
        columns={columns}
        data={rows}
        isLoading={isFetching}
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
          rangeOnly
          initialDates={clipToRange(openForm.row, range)}
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
