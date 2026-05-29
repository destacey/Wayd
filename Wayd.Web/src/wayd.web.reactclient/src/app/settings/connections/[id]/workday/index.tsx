'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  ConnectionDetailsDto,
  EmployeeMatchProperty,
  WorkdayConnectionDetailsDto,
  WorkdayWorkerKey,
} from '@/src/services/wayd-api'
import { useInitConnectionMutation } from '@/src/store/features/app-integration/connections-api'
import { Alert, Button, Space } from 'antd'
import dayjs from 'dayjs'
import { ItemType } from 'antd/es/menu/interface'
import { useState } from 'react'
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
        message="Connection is valid, but the probe noted some observations:"
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
      message="This connection's configuration is invalid. Sync is disabled until resolved."
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
            label: 'Incremental Sync',
            value: config?.incrementalSyncEnabled,
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

const TestConnectionButton = ({ ctx }: { ctx: ConnectionActionContext }) => {
  const messageApi = useMessage()
  const [initConnection, { isLoading }] = useInitConnectionMutation()
  const [isRunning, setIsRunning] = useState(false)

  const onClick = async () => {
    setIsRunning(true)
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
    } finally {
      setIsRunning(false)
    }
  }

  return (
    <Space>
      <Button
        size="small"
        onClick={onClick}
        loading={isLoading || isRunning}
        disabled={!ctx.canUpdate}
      >
        Test Connection
      </Button>
    </Space>
  )
}

// ExtraActions component variant — emits a menu item the page shell renders next to the
// activate/deactivate actions. Keeps the test-connection control in the same place admins
// already look for sync controls.
const ExtraActions = ({
  ctx,
  setItems,
}: {
  ctx: ConnectionActionContext
  setItems: (items: ItemType[]) => void
}) => {
  // Render the button into a menu item using a label-only entry pointing at our TestConnectionButton.
  // The page shell handles invocation through the menu item key, but for our case we want a button
  // directly — so we surface as a custom-label item.
  const items: ItemType[] = [
    {
      key: 'test-connection',
      label: <TestConnectionButton ctx={ctx} />,
      disabled: !ctx.canUpdate,
    },
  ]
  setItems(items)
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
