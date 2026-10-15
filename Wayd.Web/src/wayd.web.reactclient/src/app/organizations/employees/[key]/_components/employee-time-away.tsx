'use client'

import { useState } from 'react'
import { Button, Flex, Popconfirm, Typography } from 'antd'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { formatCalendarDate, isApiError, type ApiError } from '@/src/utils'
import {
  EmployeeTimeAwayDto,
  EmployeeTimeAwayEntryDto,
  useGetTimeAwayReasonsQuery,
  useRemoveEmployeeTimeAwayMutation,
} from '@/src/store/features/organization/employee-time-away-api'
import EmployeeHolidayCalendarForm, {
  SYSTEM_DEFAULT_LABEL,
} from './employee-holiday-calendar-form'
import TimeAwayForm from '@/src/app/organizations/_components/time-away/time-away-form'
import { timeAwayLabel } from '@/src/app/organizations/_components/time-away/time-away-duration'
import styles from './employee-capacity.module.css'

const { Text, Title } = Typography

// Until the read arrives; the system setting defaults to the same.
const DEFAULT_WORKING_DAY_HOURS = 8

interface Props {
  employeeId: string
  timeAway: EmployeeTimeAwayDto | undefined
  isLoading: boolean
  onRefresh: () => void
}

/** Which form is open: recording afresh, changing one stretch, or the calendar. */
type OpenForm =
  | { kind: 'record' }
  | { kind: 'change'; entry: EmployeeTimeAwayEntryDto }
  | { kind: 'calendar' }

/**
 * When a person is away from every team, and the holidays they take. Anyone
 * who can see the person sees the days; the reason, and the actions, only
 * show for those the read says may manage their time away.
 */
const EmployeeTimeAway = ({
  employeeId,
  timeAway,
  isLoading,
  onRefresh,
}: Props) => {
  const [openForm, setOpenForm] = useState<OpenForm | null>(null)
  const messageApi = useMessage()

  const { hasPermissionClaim } = useAuth()
  // Choosing a calendar also lists them, which needs their own View permission.
  const canChangeCalendar =
    hasPermissionClaim('Permissions.Employees.Update') &&
    hasPermissionClaim('Permissions.HolidayCalendars.View')
  const canManage = timeAway?.canManage === true
  const workingDayHours = timeAway?.workingDayHours ?? DEFAULT_WORKING_DAY_HOURS

  const { data: reasons } = useGetTimeAwayReasonsQuery(undefined, {
    skip: !canManage,
  })
  const [removeTimeAway] = useRemoveEmployeeTimeAwayMutation()

  const reasonName = (entry: EmployeeTimeAwayEntryDto) =>
    reasons?.find((r) => r.code === entry.reason)?.name ?? entry.reason ?? ''

  const remove = async (entry: EmployeeTimeAwayEntryDto) => {
    const response = await removeTimeAway({ employeeId, timeAwayId: entry.id })
    if (response.error) {
      const apiError: ApiError = isApiError(response.error)
        ? response.error
        : {}
      messageApi.error(apiError.detail ?? 'Failed to remove the time away.')
      return
    }
    messageApi.success('Time away removed.')
  }

  const columns: ColumnDef<EmployeeTimeAwayEntryDto, any>[] = [
    {
      id: 'start',
      accessorKey: 'start',
      header: 'First day',
      size: 120,
      cell: ({ row }) => formatCalendarDate(row.original.start),
    },
    {
      id: 'end',
      accessorKey: 'end',
      header: 'Last day',
      size: 120,
      cell: ({ row }) => formatCalendarDate(row.original.end),
    },
    {
      id: 'percentAway',
      accessorKey: 'percentAway',
      header: 'Away for',
      size: 110,
      // Read as people say it: a whole or half day, or the hours of the
      // working day the share comes to.
      cell: ({ row }) =>
        timeAwayLabel(row.original.percentAway, workingDayHours),
    },
    // The reason is only sent to those who may manage the time away, so the
    // column is left out for everyone else rather than shown empty.
    ...(canManage
      ? ([
          {
            id: 'reason',
            accessorFn: (row) => reasonName(row),
            header: 'Reason',
            size: 150,
            meta: { filterType: 'set' },
          },
          {
            id: 'actions',
            header: '',
            size: 150,
            enableSorting: false,
            enableColumnFilter: false,
            cell: ({ row }) => (
              <Flex gap="small">
                <Button
                  type="link"
                  size="small"
                  onClick={() =>
                    setOpenForm({ kind: 'change', entry: row.original })
                  }
                >
                  Change
                </Button>
                <Popconfirm
                  title="Remove this time away?"
                  okText="Remove"
                  onConfirm={() => remove(row.original)}
                >
                  <Button type="link" size="small" danger>
                    Remove
                  </Button>
                </Popconfirm>
              </Flex>
            ),
          },
        ] as ColumnDef<EmployeeTimeAwayEntryDto, any>[])
      : []),
  ]

  const closeForm = () => setOpenForm(null)

  return (
    <div className={styles.timeAway}>
      <Title level={5}>Time away</Title>
      <Flex gap="small" align="center" className={styles.holidayCalendar}>
        <Text type="secondary">Holiday calendar:</Text>
        <Text>{timeAway?.holidayCalendar?.name ?? SYSTEM_DEFAULT_LABEL}</Text>
        {canChangeCalendar && (
          <Button
            type="link"
            size="small"
            onClick={() => setOpenForm({ kind: 'calendar' })}
          >
            Change
          </Button>
        )}
      </Flex>

      <WaydGrid
        columns={columns}
        data={timeAway?.entries ?? []}
        isLoading={isLoading}
        onRefresh={onRefresh}
        leftSlot={
          canManage ? (
            <Button
              size="small"
              onClick={() => setOpenForm({ kind: 'record' })}
            >
              Record Time Away
            </Button>
          ) : undefined
        }
        persistStateKey="employee-time-away"
        csvFileName="employee-time-away"
      />

      {(openForm?.kind === 'record' || openForm?.kind === 'change') && (
        <TimeAwayForm
          employeeId={employeeId}
          workingDayHours={workingDayHours}
          timeAway={openForm.kind === 'change' ? openForm.entry : undefined}
          onFormComplete={closeForm}
          onFormCancel={closeForm}
        />
      )}

      {openForm?.kind === 'calendar' && (
        <EmployeeHolidayCalendarForm
          employeeId={employeeId}
          holidayCalendar={timeAway?.holidayCalendar}
          onFormComplete={closeForm}
          onFormCancel={closeForm}
        />
      )}
    </div>
  )
}

export default EmployeeTimeAway
