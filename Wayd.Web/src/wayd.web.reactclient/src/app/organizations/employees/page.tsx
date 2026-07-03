'use client'

import PageTitle from '@/src/components/common/page-title'
import { WaydGrid2 } from '../../../components/common/wayd-grid2'
import { useEffect, useState, useMemo } from 'react'
import { ItemType } from 'antd/es/menu/interface'
import Link from 'next/link'
import type { ColumnDef } from '@tanstack/react-table'
import { useDocumentTitle } from '../../../hooks/use-document-title'
import {
  ControlItemsMenu,
  ControlItemSwitch,
} from '../../../components/common/control-items-menu'
import { authorizePage } from '../../../components/hoc'
import { useGetEmployeesQuery } from '@/src/store/features/organizations/employee-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { EmployeeListDto } from '@/src/services/wayd-api'

const EmployeeListPage = () => {
  useDocumentTitle('Employees')
  const [includeInactive, setIncludeInactive] = useState<boolean>(false)

  const messageApi = useMessage()

  const {
    data: employeesData,
    isLoading,
    error,
    refetch,
  } = useGetEmployeesQuery(includeInactive)

  useEffect(() => {
    if (error) {
      console.error(error)
      messageApi.error('Failed to load employees.')
    }
  }, [error, messageApi])

  const columns = useMemo<ColumnDef<EmployeeListDto, any>[]>(
    () => [
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      {
        id: 'displayName',
        accessorKey: 'displayName',
        header: 'Name',
        size: 200,
        meta: { filterEnableSet: true },
        cell: ({ row }) => (
          <Link href={`/organizations/employees/${row.original.key}`}>
            {row.original.displayName}
          </Link>
        ),
      },
      { id: 'email', accessorKey: 'email', header: 'Email' },
      {
        id: 'employeeNumber',
        accessorKey: 'employeeNumber',
        header: 'Employee Number',
      },
      {
        id: 'employeeType',
        accessorKey: 'employeeType',
        header: 'Employee Type',
        meta: { filterType: 'set' },
      },
      { id: 'jobTitle', accessorKey: 'jobTitle', header: 'Job Title' },
      {
        id: 'department',
        accessorKey: 'department',
        header: 'Department',
        meta: { filterType: 'set' },
      },
      {
        id: 'manager',
        accessorKey: 'manager.name',
        header: 'Manager',
        size: 200,
        meta: { filterEnableSet: true },
        cell: ({ row }) => {
          const manager = row.original.manager
          if (!manager?.key) return manager?.name ?? null
          return (
            <Link href={`/organizations/employees/${manager.key}`}>
              {manager.name}
            </Link>
          )
        },
      },
      {
        id: 'officeLocation',
        accessorKey: 'officeLocation',
        header: 'Office Location',
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

  const refresh = async () => {
    refetch()
  }

  const onIncludeInactiveChange = (checked: boolean) => {
    setIncludeInactive(checked)
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
      <PageTitle title="Employees" />
      <WaydGrid2
        columns={columns}
        data={employeesData ?? []}
        isLoading={isLoading}
        onRefresh={refresh}
        csvFileName="employees"
        rightSlot={<ControlItemsMenu items={controlItems} />}
      />
    </>
  )
}

const EmployeeListPageWithAuthorization = authorizePage(
  EmployeeListPage,
  'Permission',
  'Permissions.Employees.View',
)

export default EmployeeListPageWithAuthorization
