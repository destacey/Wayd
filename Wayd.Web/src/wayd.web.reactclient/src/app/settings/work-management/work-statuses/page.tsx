'use client'

import { PageTitle } from '@/src/components/common'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import { useAppDispatch, useAppSelector, useDocumentTitle } from '@/src/hooks'
import { WorkStatusDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@tanstack/react-table'
import { useEffect, useMemo } from 'react'
import { setIncludeInactive } from '../../../../store/features/work-management/work-status-slice'
import { authorizePage } from '@/src/components/hoc'
import { useGetWorkStatusesQuery } from '@/src/store/features/work-management/work-status-api'
import {
  ControlItemsMenu,
  ControlItemSwitch,
} from '@/src/components/common/control-items-menu'
import { ItemType } from 'antd/es/menu/interface'

const WorkStatusesPage = () => {
  useDocumentTitle('Work Management - Work Statuses')

  const { includeInactive } = useAppSelector((state) => state.workStatus)

  const {
    data: workStatuses,
    isLoading,
    error,
    refetch,
  } = useGetWorkStatusesQuery(includeInactive)
  const dispatch = useAppDispatch()

  useEffect(() => {
    error && console.error(error)
  }, [error])

  const columns = useMemo<ColumnDef<WorkStatusDto, any>[]>(
    () => [
      { id: 'name', accessorKey: 'name', header: 'Name' },
      {
        id: 'description',
        accessorKey: 'description',
        header: 'Description',
        size: 300,
      },
      {
        id: 'isActive',
        accessorKey: 'isActive',
        header: 'Active',
        meta: { columnType: 'yesNo' },
      },
    ],
    [],
  )

  const refresh = async () => {
    refetch()
  }

  const onIncludeInactiveChange = (checked: boolean) => {
    dispatch(setIncludeInactive(checked))
    refresh()
  }

  const controlItems: ItemType[] = [
    {
      label: (
        <ControlItemSwitch
          label="Include Inactive"
          checked={includeInactive}
          onChange={onIncludeInactiveChange}
        />
      ),
      key: 'include-inactive',
      onClick: () => onIncludeInactiveChange(!includeInactive),
    },
  ]

  return (
    <>
      <PageTitle title="Work Statuses" />

      <WaydGrid
        columns={columns}
        data={workStatuses ?? []}
        onRefresh={refresh}
        isLoading={isLoading}
        csvFileName="work-statuses"
        rightSlot={<ControlItemsMenu items={controlItems} />}
      />
    </>
  )
}

const WorkStatusesPageWithAuthorization = authorizePage(
  WorkStatusesPage,
  'Permission',
  'Permissions.WorkStatuses.View',
)

export default WorkStatusesPageWithAuthorization
