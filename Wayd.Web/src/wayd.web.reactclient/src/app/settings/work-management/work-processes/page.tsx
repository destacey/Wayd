'use client'

import { PageTitle } from '@/src/components/common'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import {
  ControlItemsMenu,
  ControlItemSwitch,
} from '@/src/components/common/control-items-menu'
import { authorizePage } from '@/src/components/hoc'
import { useAppDispatch, useAppSelector, useDocumentTitle } from '@/src/hooks'
import { WorkProcessListDto } from '@/src/services/wayd-api'
import { useGetWorkProcessesQuery } from '@/src/store/features/work-management/work-process-api'
import { setIncludeInactive } from '@/src/store/features/work-management/work-process-slice'
import type { ColumnDef } from '@tanstack/react-table'
import { ItemType } from 'antd/es/menu/interface'
import Link from 'next/link'
import { useEffect, useMemo } from 'react'

const WorkProcessesPage: React.FC = () => {
  useDocumentTitle('Work Management - Work Processes')

  const { includeInactive } = useAppSelector((state) => state.workProcess)

  const {
    data: workProcessesData,
    isLoading,
    error,
    refetch,
  } = useGetWorkProcessesQuery(includeInactive)
  const dispatch = useAppDispatch()

  const columns = useMemo<ColumnDef<WorkProcessListDto, any>[]>(
    () => [
      { id: 'key', accessorKey: 'key', header: 'Key', size: 80 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        size: 300,
        cell: ({ row }) => (
          <Link href={`./work-processes/${row.original.key}`}>
            {row.original.name}
          </Link>
        ),
      },
      {
        id: 'ownership',
        accessorKey: 'ownership.name',
        header: 'Ownership',
        meta: { filterType: 'set' },
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

  useEffect(() => {
    error && console.error(error)
  }, [error])

  const refresh = async () => {
    refetch()
  }

  const onIncludeInactiveChange = (checked: boolean) => {
    dispatch(setIncludeInactive(checked))
  }

  const controlItems: ItemType[] = [
    {
      label: (
        <ControlItemSwitch
          label="Include Disabled"
          checked={includeInactive}
          onChange={onIncludeInactiveChange}
        />
      ),
      key: 'include-disabled',
      onClick: () => onIncludeInactiveChange(!includeInactive),
    },
  ]

  return (
    <>
      <PageTitle title="Work Processes" />

      <WaydGrid
        columns={columns}
        data={workProcessesData ?? []}
        onRefresh={refresh}
        isLoading={isLoading}
        csvFileName="work-processes"
        rightSlot={<ControlItemsMenu items={controlItems} />}
      />
    </>
  )
}

const WorkProcessesPageWithAuthorization = authorizePage(
  WorkProcessesPage,
  'Permission',
  'Permissions.WorkProcesses.View',
)

export default WorkProcessesPageWithAuthorization
