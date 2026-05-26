'use client'

import {
  AzureDevOpsConnectionDetailsDto,
  ConnectionDetailsDto,
} from '@/src/services/wayd-api'
import { ItemType } from 'antd/es/menu/interface'
import { useSyncAzdoConnectionOrganizationMutation } from '@/src/store/features/app-integration/azdo-integration-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError, type ApiError } from '@/src/utils'
import {
  ConnectionActionContext,
  DetailEntry,
} from '../_components/detail-registry'
import { AzdoConnectionContext } from './connection-context'
import AzdoConnectionDetails from './connection-details'
import AzdoOrganization from './organization'
import SyncHistoryTab from '../_components/sync-history-tab'
import { ReactNode, useEffect, useMemo } from 'react'

const isAzdo = (
  c: ConnectionDetailsDto,
): c is AzureDevOpsConnectionDetailsDto =>
  c?.connector?.name === 'Azure DevOps'

const Details = ({ connection }: { connection: ConnectionDetailsDto }) => {
  if (!isAzdo(connection)) return null
  return <AzdoConnectionDetails connection={connection} />
}

const AzdoWrapper = ({
  connection,
  reload,
  children,
}: {
  connection: ConnectionDetailsDto
  reload: () => void
  children: ReactNode
}) => {
  const orgUrl = isAzdo(connection)
    ? connection.configuration?.organizationUrl
    : undefined
  return (
    <AzdoConnectionContext.Provider
      value={{
        connectionId: connection.id,
        organizationUrl: orgUrl,
        reloadConnectionData: (() => reload()) as never,
      }}
    >
      {children}
    </AzdoConnectionContext.Provider>
  )
}

const AzdoExtraActions = ({
  ctx,
  setItems,
}: {
  ctx: ConnectionActionContext
  setItems: (items: ItemType[]) => void
}) => {
  const messageApi = useMessage()
  const [syncOrganization] = useSyncAzdoConnectionOrganizationMutation()

  const azdo = isAzdo(ctx.connection) ? ctx.connection : null
  const isValidConfiguration = azdo?.isValidConfiguration ?? false
  const canUpdate = ctx.canUpdate

  const items = useMemo<ItemType[]>(() => {
    if (!azdo || !canUpdate) return []

    const syncOrgConfig = async () => {
      try {
        const response = await syncOrganization(ctx.connectionId)
        if (response.error) throw response.error
        messageApi.success(
          'Successfully imported organization processes and projects.',
        )
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        console.error(error)
        messageApi.error(
          `Failed to initialize organization. Error: ${apiError.detail}`,
        )
      }
    }

    return [
      {
        key: 'sync-organization',
        label: 'Sync Organization Configuration',
        disabled: !isValidConfiguration,
        onClick: () => syncOrgConfig(),
      },
    ]
  }, [
    azdo,
    canUpdate,
    ctx.connectionId,
    isValidConfiguration,
    messageApi,
    syncOrganization,
  ])

  useEffect(() => {
    setItems(items)
  }, [items, setItems])

  return null
}

export const azureDevOpsDetailEntry: DetailEntry = {
  Details,
  Wrapper: AzdoWrapper,
  extraTabs: [
    {
      key: 'organization-configuration',
      label: 'Organization Configuration',
      render: (connection) => {
        if (!isAzdo(connection)) return null
        return (
          <AzdoOrganization
            workProcesses={connection.configuration?.workProcesses ?? []}
            workspaces={connection.configuration?.workspaces ?? []}
          />
        )
      },
    },
    {
      key: 'sync-history',
      label: 'Sync History',
      render: (connection) => (
        <SyncHistoryTab connectionId={connection.id} category="work" />
      ),
    },
  ],
  ExtraActions: AzdoExtraActions,
  getExternalUrl: (connection) =>
    isAzdo(connection) ? connection.configuration?.organizationUrl : undefined,
}
