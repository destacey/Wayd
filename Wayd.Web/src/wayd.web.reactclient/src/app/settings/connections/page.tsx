'use client'

import { useMemo, useState } from 'react'
import { PageActions, PageTitle } from '../../../components/common'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import { authorizePage } from '../../../components/hoc'
import { useDocumentTitle } from '../../../hooks'
import useAuth from '../../../components/contexts/auth'
import CreateConnectionForm from './_components/create-connection-form'
import Link from 'next/link'
import { ConnectionListDto } from '@/src/services/wayd-api'
import { getCapabilityNames } from '@/src/types/connectors'
import type { ColumnDef } from '@tanstack/react-table'
import { ItemType } from 'antd/es/menu/interface'
import { useGetConnectionsQuery } from '@/src/store/features/app-integration/connections-api'
import {
  ControlItemsMenu,
  ControlItemSwitch,
} from '../../../components/common/control-items-menu'

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

  const columns = useMemo<ColumnDef<ConnectionListDto, any>[]>(
    () => [
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        size: 250,
        cell: ({ row }) => (
          <Link href={`/settings/connections/${row.original.id}`}>
            {row.original.name}
          </Link>
        ),
      },
      {
        id: 'connector',
        accessorKey: 'connector.name',
        header: 'Connector',
        size: 150,
        meta: { filterType: 'set' },
      },
      {
        id: 'capabilities',
        accessorFn: (row) => getCapabilityNames(row),
        header: 'Capabilities',
        size: 180,
      },
      {
        id: 'isActive',
        accessorKey: 'isActive',
        header: 'Active',
        size: 125,
        meta: { columnType: 'yesNo' },
      },
      {
        id: 'isValidConfiguration',
        accessorKey: 'isValidConfiguration',
        header: 'Valid Configuration',
        size: 160,
        meta: { columnType: 'yesNo' },
      },
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
        columns={columns}
        data={connectionsData ?? []}
        onRefresh={refresh}
        isLoading={isLoading}
        persistStateKey="settings-connections"
        csvFileName="connections"
        rightSlot={<ControlItemsMenu items={controlItems} />}
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
