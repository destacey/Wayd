'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  ConnectionDetailsDto,
  EmployeeMatchProperty,
  WorkdayConnectionDetailsDto,
  WorkdayWorkerKey,
} from '@/src/services/wayd-api'
import { useInitConnectionMutation } from '@/src/store/features/app-integration/connections-api'
import { Alert } from 'antd'
import dayjs from 'dayjs'
import { ItemType } from 'antd/es/menu/interface'
import { useEffect, useMemo } from 'react'
import {
  ConnectionActionContext,
  DetailEntry,
} from '../_components/detail-registry'
import GenericConnectionDetails from '../_components/generic-connection-details'
import SyncHistoryTab from '../_components/sync-history-tab'

const isWorkday = (c: ConnectionDetailsDto): c is WorkdayConnectionDetailsDto =>
  c?.connector?.name === 'Workday'

const workerKeyLabel = (key: WorkdayWorkerKey | undefined): string => {
  switch (key) {
    case WorkdayWorkerKey.Wid:
      return 'Workday Worker ID'
    case WorkdayWorkerKey.EmployeeId:
      return 'Employee ID'
    default:
      return '—'
  }
}

const InitResultCallout = ({
  connection,
}: {
  connection: WorkdayConnectionDetailsDto
}) => {
  const config = connection.configuration
  if (!config?.lastInitAt) return null

  if (config.lastInitSucceeded) {
    if ((config.lastInitWarnings?.length ?? 0) === 0) return null
    return (
      <Alert
        type="warning"
        showIcon
        style={{ marginBottom: 16 }}
        title="Connection is valid, but the probe noted some observations:"
        description={
          <ul style={{ marginBottom: 0 }}>
            {config.lastInitWarnings?.map((w, i) => <li key={i}>{w}</li>)}
          </ul>
        }
      />
    )
  }

  return (
    <Alert
      type="error"
      showIcon
      style={{ marginBottom: 16 }}
      title="This connection's configuration is invalid. Sync is disabled until resolved."
      description={
        <>
          {config.lastInitAuthError && <p>{config.lastInitAuthError}</p>}
          {(config.lastInitMissingFields?.length ?? 0) > 0 && (
            <>
              <p>
                The Integration System User could not read these required
                fields — likely an Integration System Security Group (ISSG) gap
                in Workday:
              </p>
              <ul style={{ marginBottom: 0 }}>
                {config.lastInitMissingFields?.map((f, i) => <li key={i}>{f}</li>)}
              </ul>
            </>
          )}
          {(config.lastInitWarnings?.length ?? 0) > 0 && (
            <>
              <p>Additional warnings:</p>
              <ul style={{ marginBottom: 0 }}>
                {config.lastInitWarnings?.map((w, i) => <li key={i}>{w}</li>)}
              </ul>
            </>
          )}
        </>
      }
    />
  )
}

const Details = ({ connection }: { connection: ConnectionDetailsDto }) => {
  if (!isWorkday(connection)) return null
  const config = connection.configuration

  return (
    <>
      <InitResultCallout connection={connection} />
      <GenericConnectionDetails
        connection={connection}
        configFields={[
          { label: 'WSDL URL', value: config?.wsdlUrl },
          { label: 'Service Host', value: config?.serviceHost },
          { label: 'Tenant Alias', value: config?.tenantAlias },
          { label: 'WSDL Version', value: config?.wsdlVersion },
          { label: 'ISU Username', value: config?.isuUsername },
          { label: 'ISU Password', value: config?.isuPassword, sensitive: true },
          { label: 'Worker Key', value: workerKeyLabel(config?.workerKey) },
          {
            label: 'Match Employees By',
            value:
              config?.matchBy === EmployeeMatchProperty.EmployeeNumber
                ? 'Employee Number'
                : 'Email',
          },
          { label: 'Include Inactive Workers', value: config?.includeInactive },
          {
            label: 'Use User_ID as Email Fallback',
            value: config?.useUserIdAsEmailFallback,
          },
          {
            label: 'Use Preferred Name',
            value: config?.usePreferredName,
          },
          {
            label: 'Last Validated',
            value: config?.lastInitAt
              ? dayjs(config.lastInitAt).format('M/D/YYYY h:mm A')
              : undefined,
          },
        ]}
      />
    </>
  )
}

// Adds a "Test Connection" entry to the page shell's Actions dropdown. We model it as a plain
// menu item (text label + onClick) rather than embedding a <Button> in the label — the shell
// renders the menu with antd's Menu, which would otherwise show a button-inside-a-menu-row.
const ExtraActions = ({
  ctx,
  setItems,
}: {
  ctx: ConnectionActionContext
  setItems: (items: ItemType[]) => void
}) => {
  const messageApi = useMessage()
  const [initConnection] = useInitConnectionMutation()

  const items = useMemo<ItemType[]>(() => {
    const testConnection = async () => {
      try {
        const response = await initConnection(ctx.connectionId)
        if ('error' in response) throw response.error
        const result = response.data
        if (result.isValid) {
          messageApi.success(
            `Connection validated — probed ${result.workersProbed} worker(s).`,
          )
        } else if (result.authError) {
          messageApi.error(`Authentication failed: ${result.authError}`)
        } else {
          messageApi.error(
            `Configuration is invalid (${result.missingRequiredFields.length} missing field(s)). See the connection details for specifics.`,
          )
        }
        ctx.reload()
      } catch (e) {
        console.error(e)
        messageApi.error('Failed to run the Workday connection probe.')
      }
    }

    return [
      {
        key: 'test-connection',
        label: 'Test Connection',
        disabled: !ctx.canUpdate,
        onClick: testConnection,
      },
    ]
  }, [ctx, initConnection, messageApi])

  // setItems updates state on the parent page; React forbids calling it during render of a
  // child, so we defer to an effect.
  useEffect(() => {
    setItems(items)
  }, [items, setItems])

  return null
}

export const workdayDetailEntry: DetailEntry = {
  Details,
  ExtraActions,
  extraTabs: [
    {
      key: 'sync-history',
      label: 'Sync History',
      render: (connection) => (
        <SyncHistoryTab
          connectionId={connection.id}
          category="people"
          isActive={connection.isActive}
        />
      ),
    },
  ],
}
