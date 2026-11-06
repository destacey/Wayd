'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  ConnectionDetailsDto,
  TeamsConnectionDetailsDto,
} from '@/src/services/wayd-api'
import { useTestCommunicationConnectionMutation } from '@/src/store/features/app-integration/connections-api'
import { toApiError } from '@/src/utils'
import { Collapse } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { useEffect, useMemo } from 'react'
import {
  DetailEntry,
  ConnectionActionContext,
} from '../_components/detail-registry'
import GenericConnectionDetails from '../_components/generic-connection-details'
import { TeamsSetupGuide } from '../../_components/teams-setup-guide'

const isTeams = (c: ConnectionDetailsDto) =>
  c?.connector?.name === 'Microsoft Teams'

const Details = ({ connection }: { connection: ConnectionDetailsDto }) => {
  if (!isTeams(connection)) return null
  const config = (connection as TeamsConnectionDetailsDto).configuration
  return (
    <>
      <Collapse
        style={{ marginBottom: 24 }}
        items={[
          {
            key: 'setup',
            label: 'Microsoft Teams setup',
            children: <TeamsSetupGuide />,
          },
        ]}
      />
      <GenericConnectionDetails
        connection={connection}
        configFields={[
          { label: 'Tenant ID', value: config?.tenantId },
          { label: 'Client ID', value: config?.clientId },
          {
            label: 'Client Secret',
            value: config?.clientSecret,
            sensitive: true,
          },
        ]}
      />
    </>
  )
}

const ExtraActions = ({
  ctx,
  setItems,
}: {
  ctx: ConnectionActionContext
  setItems: (items: ItemType[]) => void
}) => {
  const messageApi = useMessage()
  const [testConnection, { isLoading }] =
    useTestCommunicationConnectionMutation()

  const items = useMemo<ItemType[]>(() => {
    const runTest = async () => {
      try {
        const response = await testConnection({
          id: ctx.connectionId,
        })
        if ('error' in response) throw response.error
        messageApi.success(
          'Connection test sent. Check Teams for a chat named Wayd connection test and your calendar for the test event.',
        )
      } catch (error: unknown) {
        const apiError = toApiError(error)
        messageApi.error(
          apiError.detail ??
            'The connection test failed. Confirm admin consent, permissions, and that your Wayd email exists in the tenant.',
        )
      }
    }

    return [
      {
        key: 'test-connection',
        label: isLoading ? 'Testing connection…' : 'Test connection',
        disabled: !ctx.canUpdate || isLoading,
        onClick: runTest,
      },
    ]
  }, [ctx.canUpdate, ctx.connectionId, isLoading, messageApi, testConnection])

  useEffect(() => {
    setItems(items)
  }, [items, setItems])

  return null
}

export const teamsDetailEntry: DetailEntry = {
  Details,
  ExtraActions,
}
