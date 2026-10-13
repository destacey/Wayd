'use client'

import { useState } from 'react'
import { Button, Flex, Progress, Statistic, Typography } from 'antd'
import { WaydGrid, renderTeamLink } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import useAuth from '@/src/components/contexts/auth'
import { formatCalendarDate, todayCalendarDate } from '@/src/utils'
import {
  EmployeeAllocationDto,
  useGetEmployeeAllocationsQuery,
} from '@/src/store/features/organization/employee-allocations-api'
import { allocatedOn, allocationPeriods } from './allocation-periods'
import ChangeAllocationForm, {
  AllocationToChange,
} from './change-allocation-form'
import MoveAllocationForm from './move-allocation-form'
import styles from './employee-capacity.module.css'

const { Text } = Typography

const FULL_ALLOCATION = 100

interface Props {
  employeeId: string
}

/** Which form is open: allocating afresh, changing one allocation, or moving time. */
type OpenForm =
  | { kind: 'allocate' }
  | { kind: 'change'; allocation: AllocationToChange }
  | { kind: 'move' }

const EmployeeCapacity = ({ employeeId }: Props) => {
  const [openForm, setOpenForm] = useState<OpenForm | null>(null)

  const { hasPermissionClaim } = useAuth()
  const canUpdateTeams = hasPermissionClaim('Permissions.Teams.Update')

  const { data, isLoading, refetch } = useGetEmployeeAllocationsQuery(
    { employeeId },
    { skip: !employeeId },
  )
  const allocations = data ?? []

  const today = todayCalendarDate()
  const allocatedToday = allocatedOn(allocations, today)
  const periods = allocationPeriods(allocations, today)
  const canMove = canUpdateTeams && allocations.some((a) => a.canManage)

  const columns: ColumnDef<EmployeeAllocationDto, any>[] = [
    {
      id: 'team',
      accessorKey: 'team.name',
      header: 'Team',
      size: 220,
      meta: { filterEnableSet: true },
      cell: ({ row }) => renderTeamLink(row.original.team),
    },
    {
      id: 'type',
      accessorKey: 'team.type',
      header: 'Type',
      size: 140,
      meta: { filterType: 'set' },
    },
    {
      id: 'role',
      accessorFn: (row) => row.role?.name ?? '',
      header: 'Role',
      size: 180,
      meta: { filterEnableSet: true },
    },
    {
      id: 'start',
      accessorKey: 'start',
      header: 'Start',
      size: 130,
      cell: ({ row }) => formatCalendarDate(row.original.start),
    },
    {
      id: 'end',
      accessorFn: (row) => row.end ?? '',
      header: 'End',
      size: 130,
      cell: ({ row }) =>
        row.original.end ? formatCalendarDate(row.original.end) : 'No end',
    },
    {
      id: 'percent',
      accessorKey: 'percent',
      header: 'Percent',
      size: 110,
      cell: ({ row }) => `${row.original.percent}%`,
    },
    {
      id: 'actions',
      header: '',
      size: 90,
      enableSorting: false,
      enableColumnFilter: false,
      cell: ({ row }) =>
        canUpdateTeams && row.original.canManage ? (
          <Button
            type="link"
            size="small"
            onClick={() =>
              setOpenForm({
                kind: 'change',
                allocation: {
                  teamId: row.original.team.id,
                  roleId: row.original.role?.id,
                  start: row.original.start,
                  end: row.original.end,
                  percent: row.original.percent,
                },
              })
            }
          >
            Change
          </Button>
        ) : null,
    },
  ]

  const closeForm = () => setOpenForm(null)

  return (
    <div>
      <Flex gap="large" wrap className={styles.summary}>
        <Statistic title="Allocated today" value={allocatedToday} suffix="%" />
        <Statistic
          title="Unallocated today"
          value={Math.max(FULL_ALLOCATION - allocatedToday, 0)}
          suffix="%"
        />
      </Flex>

      <div className={styles.periods}>
        <Text strong>Total allocation from today</Text>
        {periods.map((period) => (
          <Flex key={period.start} align="center" gap="middle">
            <Text className={styles.periodDates}>
              {formatCalendarDate(period.start)}
              {' – '}
              {period.end ? formatCalendarDate(period.end) : 'No end'}
            </Text>
            <Progress
              className={styles.periodBar}
              percent={period.percent}
              format={(percent) => `${percent}%`}
            />
          </Flex>
        ))}
      </div>

      <WaydGrid
        columns={columns}
        data={allocations}
        isLoading={isLoading}
        onRefresh={() => {
          refetch()
        }}
        leftSlot={
          canUpdateTeams ? (
            <Flex gap="small">
              <Button
                size="small"
                onClick={() => setOpenForm({ kind: 'allocate' })}
              >
                Allocate
              </Button>
              <Button
                size="small"
                disabled={!canMove}
                onClick={() => setOpenForm({ kind: 'move' })}
              >
                Move
              </Button>
            </Flex>
          ) : undefined
        }
        persistStateKey="employee-allocations"
        csvFileName="employee-allocations"
      />

      {(openForm?.kind === 'allocate' || openForm?.kind === 'change') && (
        <ChangeAllocationForm
          employeeId={employeeId}
          allocation={
            openForm.kind === 'change' ? openForm.allocation : undefined
          }
          onFormComplete={closeForm}
          onFormCancel={closeForm}
        />
      )}

      {openForm?.kind === 'move' && (
        <MoveAllocationForm
          employeeId={employeeId}
          allocations={allocations}
          onFormComplete={closeForm}
          onFormCancel={closeForm}
        />
      )}
    </div>
  )
}

export default EmployeeCapacity
