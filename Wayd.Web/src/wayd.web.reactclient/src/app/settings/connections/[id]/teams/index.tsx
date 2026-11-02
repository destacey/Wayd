'use client'

import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  ConnectionDetailsDto,
  TeamsConnectionDetailsDto,
} from '@/src/services/wayd-api'
import { useGetAuthProvidersQuery } from '@/src/store/features/common/auth-providers-api'
import { useTestCommunicationConnectionMutation } from '@/src/store/features/app-integration/connections-api'
import { getOidcSubjectAccessToken } from '../../_components/teams-entra-token'
import { Collapse } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { useEffect, useMemo } from 'react'
import { DetailEntry, ConnectionActionContext } from '../_components/detail-registry'
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
          { label: 'Client Secret', value: config?.clientSecret, sensitive: true },
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
  const { loginProvider } = useAuth()
  const { data: authProviders } = useGetAuthProvidersQuery()
  const [testConnection, { isLoading }] = useTestCommunicationConnectionMutation()

  const items = useMemo<ItemType[]>(() => {
    const runTest = async () => {
      try {
        const config = (ctx.connection as TeamsConnectionDetailsDto).configuration
        const subject = await getOidcSubjectAccessToken(
          loginProvider,
          authProviders?.oidc,
          config?.clientId ?? '',
        )
        if ('error' in subject) {
          messageApi.error(subject.error)
          return
        }
        const subjectToken = subject.token

        const response = await testConnection({
          id: ctx.connectionId,
          subjectToken,
        })
        if ('error' in response) throw response.error
        messageApi.success(
          'Connection test sent. Check Teams for the hello-world message and your calendar for the test event.',
        )
      } catch (e: unknown) {
        console.error(e)
        messageApi.error(
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
  }, [
    ctx.canUpdate,
    ctx.connectionId,
    isLoading,
    messageApi,
    testConnection,
    loginProvider,
    authProviders?.oidc,
  ])

  useEffect(() => {
    setItems(items)
  }, [items, setItems])

  return null
}

export const teamsDetailEntry: DetailEntry = {
  Details,
  ExtraActions,
}
