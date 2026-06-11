'use client'

import { useMemo, useState } from 'react'
import { WaydGrid, PageActions, PageTitle } from '../../../components/common'
import { authorizePage } from '../../../components/hoc'
import { useDocumentTitle } from '../../../hooks'
import useAuth from '../../../components/contexts/auth'
import CreateConnectionForm from './_components/create-connection-form'
import Link from 'next/link'
import { ConnectionListDto } from '@/src/services/wayd-api'
import { getCapabilityNames } from '@/src/types/connectors'
import { ColDef, ICellRendererParams } from 'ag-grid-community'
import { ItemType } from 'antd/es/menu/interface'
import { useGetConnectionsQuery } from '@/src/store/features/app-integration/connections-api'
import { ControlItemSwitch } from '../../../components/common/control-items-menu'

const ConnectionLinkCellRenderer = ({
  value,
  data,
}: ICellRendererParams<ConnectionListDto>) => {
  return <Link href={`/settings/connections/${data!.id}`}>{value}</Link>
}

const ConnectionsPage = () => {
  useDocumentTitle('Connections')
  const [openCreateConnectionForm, setOpenCreateConnectionForm] =
    useState(false)
  const [includeDisabled, setIncludeDisabled] = useState(false)

  const {
    data: connectionsData,
    isLoading,
    refetch,
  } = useGetConnectionsQuery(includeDisabled)

  const { hasPermissionClaim } = useAuth()
  const canCreateConnection = hasPermissionClaim(
    'Permissions.Connections.Create',
  )

  const columnDefs = useMemo<ColDef<ConnectionListDto>[]>(
    () => [
      { field: 'id', hide: true },
      { field: 'name', cellRenderer: ConnectionLinkCellRenderer, width: 250 },
      { field: 'connector.name', headerName: 'Connector', width: 150 },
      {
        headerName: 'Capabilities',
        valueGetter: ({ data }) => getCapabilityNames(data),
        width: 180,
      },
      { field: 'isActive', width: 125 },
      { field: 'isValidConfiguration', width: 150 },
    ],
    [],
  )

  const actionsMenuItems = (() => {
    const items = [] as ItemType[]
    if (canCreateConnection) {
      items.push({
        key: 'create-connection-menu-item',
        label: 'Create Connection',
        onClick: () => setOpenCreateConnectionForm(true),
      })
    }
    return items
  })()

  const refresh = async () => {
    refetch()
  }

  const onIncludeDisabledChange = (checked: boolean) => {
    setIncludeDisabled(checked)
  }

  const controlItems: ItemType[] = [
    {
      label: (
        <ControlItemSwitch
          label="Include Disabled"
          checked={includeDisabled}
          onChange={onIncludeDisabledChange}
        />
      ),
      key: 'include-disabled',
      onClick: () => onIncludeDisabledChange(!includeDisabled),
    },
  ]

  return (
    <>
      <PageTitle
        title="Connections"
        actions={<PageActions actionItems={actionsMenuItems} />}
      />

      <WaydGrid
        height={600}
        columnDefs={columnDefs}
        gridControlMenuItems={controlItems}
        rowData={connectionsData}
        loadData={refresh}
        loading={isLoading}
      />

      {openCreateConnectionForm && (
        <CreateConnectionForm
          onFormCreate={() => setOpenCreateConnectionForm(false)}
          onFormCancel={() => setOpenCreateConnectionForm(false)}
        />
      )}
    </>
  )
}

const PageWithAuthorization = authorizePage(
  ConnectionsPage,
  'Permission',
  'Permissions.Connections.View',
)

export default PageWithAuthorization
