'use client'

import PageTitle from '@/src/components/common/page-title'
import WaydGrid from '../../../components/common/wayd-grid'
import { useEffect, useState, useMemo } from 'react'
import { ItemType } from 'antd/es/menu/interface'
import Link from 'next/link'
import { useDocumentTitle } from '../../../hooks/use-document-title'
import { ControlItemSwitch } from '../../../components/common/control-items-menu'
import { authorizePage } from '../../../components/hoc'
import { useGetEmployeesQuery } from '@/src/store/features/organizations/employee-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { EmployeeListDto } from '@/src/services/wayd-api'
import { ICellRendererParams } from 'ag-grid-community'

const EmployeeLinkCellRenderer = ({
  value,
  data,
}: ICellRendererParams<EmployeeListDto>) => {
  return <Link href={`/organizations/employees/${data!.key}`}>{value}</Link>
}

const ManagerLinkCellRenderer = ({
  value,
  data,
}: ICellRendererParams<EmployeeListDto>) => {
  if (!data?.manager?.key) return value ?? null
  return (
    <Link href={`/organizations/employees/${data.manager.key}`}>{value}</Link>
  )
}

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

  const columnDefs = useMemo(
    () => [
      { field: 'key', width: 90 },
      {
        field: 'displayName',
        headerName: 'Name',
        cellRenderer: EmployeeLinkCellRenderer,
      },
      { field: 'email' },
      { field: 'employeeNumber', headerName: 'Employee Number' },
      { field: 'employeeType', headerName: 'Employee Type' },
      { field: 'jobTitle' },
      { field: 'department' },
      {
        field: 'manager.name',
        headerName: 'Manager',
        cellRenderer: ManagerLinkCellRenderer,
      },
      { field: 'officeLocation' },
      { field: 'isActive' }, // TODO: convert to yes/no
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
      <WaydGrid
        columnDefs={columnDefs}
        gridControlMenuItems={controlItems}
        rowData={employeesData}
        loading={isLoading}
        loadData={refresh}
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
