'use client'

import { PageTitle } from '@/src/components/common'
import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { HolidayCalendarListDto } from '@/src/services/wayd-api'
import { useGetHolidayCalendarsQuery } from '@/src/store/features/organization/holiday-calendars-api'
import { isApiError } from '@/src/utils'
import { Button, Tag } from 'antd'
import Link from 'next/link'
import { useRouter } from 'next/navigation'
import { useEffect, useMemo, useState } from 'react'
import DeleteHolidayCalendarForm from './_components/delete-holiday-calendar-form'
import HolidayCalendarForm from './_components/holiday-calendar-form'

const HolidayCalendarsPage = () => {
  useDocumentTitle('Organization - Holiday Calendars')

  const [openCreateForm, setOpenCreateForm] = useState(false)
  const [editing, setEditing] = useState<HolidayCalendarListDto | null>(null)
  const [deleting, setDeleting] = useState<HolidayCalendarListDto | null>(null)

  const messageApi = useMessage()
  const router = useRouter()
  const { hasPermissionClaim } = useAuth()
  const canCreate = hasPermissionClaim('Permissions.HolidayCalendars.Create')
  const canUpdate = hasPermissionClaim('Permissions.HolidayCalendars.Update')
  const canDelete = hasPermissionClaim('Permissions.HolidayCalendars.Delete')

  const {
    data: calendars,
    isLoading,
    error,
    refetch,
  } = useGetHolidayCalendarsQuery()

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading holiday calendars.',
      )
      console.error(error)
    }
  }, [error, messageApi])

  const columns = useMemo<ColumnDef<HolidayCalendarListDto, any>[]>(
    () => [
      createActionsColumn<HolidayCalendarListDto>({
        unavailable: !canUpdate && !canDelete,
        ariaLabel: 'Holiday calendar actions',
        getItems: (calendar) => [
          ...(canUpdate
            ? [
                {
                  key: 'edit',
                  label: 'Edit',
                  onClick: () => setEditing(calendar),
                },
              ]
            : []),
          ...(canDelete
            ? [
                {
                  key: 'delete',
                  label: 'Delete',
                  danger: true,
                  onClick: () => setDeleting(calendar),
                },
              ]
            : []),
        ],
      }),
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        size: 250,
        cell: ({ row }) => (
          <Link
            href={`/settings/organization/holiday-calendars/${row.original.key}`}
          >
            {row.original.name}
          </Link>
        ),
      },
      {
        id: 'isDefault',
        accessorFn: (row) => (row.isDefault ? 'Default' : ''),
        header: 'Default',
        size: 110,
        cell: ({ row }) =>
          row.original.isDefault ? <Tag color="blue">Default</Tag> : null,
      },
      {
        id: 'holidayCount',
        accessorKey: 'holidayCount',
        header: 'Holidays',
        size: 110,
      },
      {
        id: 'description',
        accessorKey: 'description',
        header: 'Description',
        size: 400,
      },
    ],
    [canUpdate, canDelete],
  )

  const actions = canCreate ? (
    <Button onClick={() => setOpenCreateForm(true)}>
      Create Holiday Calendar
    </Button>
  ) : null

  return (
    <div className="page-gutters">
      <PageTitle
        title="Holiday Calendars"
        subtitle="Days a whole team is off. A team's ideal burn-down stays flat on its calendar's holidays."
        actions={actions}
      />

      <WaydGrid
        columns={columns}
        data={calendars ?? []}
        onRefresh={() => {
          refetch()
        }}
        isLoading={isLoading}
        persistStateKey="settings-holiday-calendars"
        csvFileName="holiday-calendars"
      />

      {openCreateForm && (
        <HolidayCalendarForm
          onFormComplete={(created) => {
            setOpenCreateForm(false)
            if (created) {
              router.push(
                `/settings/organization/holiday-calendars/${created.key}`,
              )
            }
          }}
          onFormCancel={() => setOpenCreateForm(false)}
        />
      )}
      {editing && (
        <HolidayCalendarForm
          calendar={editing}
          onFormComplete={() => setEditing(null)}
          onFormCancel={() => setEditing(null)}
        />
      )}
      {deleting && (
        <DeleteHolidayCalendarForm
          calendar={deleting}
          onFormComplete={() => setDeleting(null)}
          onFormCancel={() => setDeleting(null)}
        />
      )}
    </div>
  )
}

const HolidayCalendarsPageWithAuthorization = authorizePage(
  HolidayCalendarsPage,
  'Permission',
  'Permissions.HolidayCalendars.View',
)

export default HolidayCalendarsPageWithAuthorization
