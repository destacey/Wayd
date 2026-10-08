'use client'

import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { useMessage } from '@/src/components/contexts/messaging'
import { HolidayDto } from '@/src/services/wayd-api'
import { useRemoveHolidayMutation } from '@/src/store/features/organization/holiday-calendars-api'
import { formatCalendarDate, isApiError } from '@/src/utils'
import { Modal } from 'antd'
import { useMemo, useState } from 'react'
import HolidayForm from './holiday-form'

export interface HolidaysGridProps {
  calendarId: string
  holidays: HolidayDto[]
  canUpdate: boolean
  onRefresh: () => void
}

/** A calendar's holidays in date order, with the weekday each falls on. */
const HolidaysGrid = ({
  calendarId,
  holidays,
  canUpdate,
  onRefresh,
}: HolidaysGridProps) => {
  const messageApi = useMessage()
  const [editing, setEditing] = useState<HolidayDto | null>(null)
  const [removeHoliday] = useRemoveHolidayMutation()

  const columns = useMemo<ColumnDef<HolidayDto, any>[]>(() => {
    const confirmRemove = (holiday: HolidayDto) =>
      Modal.confirm({
        title: 'Remove Holiday',
        content: `Remove ${holiday.name} on ${formatCalendarDate(holiday.date)}? Teams using this calendar will work that day.`,
        okText: 'Remove',
        okType: 'danger',
        onOk: async () => {
          const response = await removeHoliday({
            calendarId,
            holidayId: holiday.id,
          })
          if (response.error) {
            messageApi.error(
              (isApiError(response.error)
                ? response.error.detail
                : undefined) ?? 'Failed to remove the holiday.',
            )
          } else {
            messageApi.success('Holiday removed.')
          }
        },
      })

    return [
      createActionsColumn<HolidayDto>({
        unavailable: !canUpdate,
        ariaLabel: 'Holiday actions',
        getItems: (holiday) => [
          { key: 'edit', label: 'Edit', onClick: () => setEditing(holiday) },
          {
            key: 'remove',
            label: 'Remove',
            danger: true,
            onClick: () => confirmRemove(holiday),
          },
        ],
      }),
      {
        id: 'date',
        accessorKey: 'date',
        header: 'Date',
        size: 140,
        meta: { columnType: 'dateOnly' },
      },
      {
        id: 'weekday',
        accessorFn: (row) => formatCalendarDate(row.date, 'dddd'),
        header: 'Weekday',
        size: 130,
        meta: { filterType: 'set' },
      },
      {
        id: 'year',
        accessorFn: (row) => formatCalendarDate(row.date, 'YYYY'),
        header: 'Year',
        size: 100,
        meta: { filterType: 'set' },
      },
      { id: 'name', accessorKey: 'name', header: 'Name', size: 300 },
    ]
  }, [calendarId, canUpdate, messageApi, removeHoliday])

  return (
    <>
      <WaydGrid
        columns={columns}
        data={holidays}
        onRefresh={onRefresh}
        persistStateKey="settings-holiday-calendar-holidays"
        csvFileName="holidays"
      />
      {editing && (
        <HolidayForm
          calendarId={calendarId}
          holiday={editing}
          onFormComplete={() => setEditing(null)}
          onFormCancel={() => setEditing(null)}
        />
      )}
    </>
  )
}

export default HolidaysGrid
