'use client'

import { OidcProviderDto, OidcProviderType } from '@/src/services/wayd-api'
import { useTestOidcProviderDiscoveryMutation } from '@/src/store/features/user-management/oidc-providers-api'
import { useGetRolesQuery } from '@/src/store/features/user-management/roles-api'
import {
  CheckCircleOutlined,
  CloseCircleOutlined,
  LoadingOutlined,
} from '@ant-design/icons'
import {
  Alert,
  Button,
  Card,
  Col,
  Descriptions,
  Flex,
  Row,
  Space,
  Typography,
} from 'antd'
import { useState } from 'react'

const { Item } = Descriptions
const { Text } = Typography

const providerTypeLabel = (type: string) => {
  if (type === OidcProviderType.MicrosoftEntraId) return 'Microsoft Entra ID'
  if (type === OidcProviderType.GenericOidc) return 'Generic OIDC'
  return type
}

interface DiscoveryState {
  status: 'idle' | 'loading' | 'success' | 'error'
  issuer?: string
  jwksKeyCount?: number
  error?: string
}

const DiscoveryTest = ({ providerId }: { providerId: string }) => {
  const [state, setState] = useState<DiscoveryState>({ status: 'idle' })
  const [testDiscovery] = useTestOidcProviderDiscoveryMutation()

  const handleTest = async () => {
    setState({ status: 'loading' })
    try {
      const response = await testDiscovery(providerId)
      if (response.error) throw response.error
      const result = response.data!
      if (result.success) {
        setState({
          status: 'success',
          issuer: result.issuer,
          jwksKeyCount: result.jwksKeyCount,
        })
      } else {
        setState({ status: 'error', error: result.error })
      }
    } catch {
      setState({ status: 'error', error: 'Request failed' })
    }
  }

  return (
    <Card size="small" title="Discovery">
      <Flex vertical gap="small" align="flex-start">
        <Button
          size="small"
          onClick={handleTest}
          loading={state.status === 'loading'}
        >
          Test discovery
        </Button>
        {state.status === 'success' && (
          <Space size={4}>
            <CheckCircleOutlined
              style={{ color: 'var(--ant-color-success)' }}
            />
            <Text style={{ color: 'var(--ant-color-success)' }}>
              Reachable — issuer {state.issuer}, {state.jwksKeyCount} signing
              key(s)
            </Text>
          </Space>
        )}
        {state.status === 'error' && (
          <Space size={4}>
            <CloseCircleOutlined style={{ color: 'var(--ant-color-error)' }} />
            <Text style={{ color: 'var(--ant-color-error)' }}>
              {state.error ?? 'Discovery failed'}
            </Text>
          </Space>
        )}
        {state.status === 'loading' && (
          <Space size={4}>
            <LoadingOutlined style={{ color: 'var(--ant-color-primary)' }} />
            <Text type="secondary">Testing the discovery endpoint…</Text>
          </Space>
        )}
      </Flex>
    </Card>
  )
}

interface OidcProviderDetailsProps {
  provider: OidcProviderDto
}

const OidcProviderDetails = ({ provider }: OidcProviderDetailsProps) => {
  const { data: roles } = useGetRolesQuery()

  const isMicrosoftEntraId =
    provider.providerType === OidcProviderType.MicrosoftEntraId

  const defaultRoleName = provider.defaultRoleId
    ? (roles?.find((r) => r.id === provider.defaultRoleId)?.name ??
      provider.defaultRoleId)
    : null

  return (
    <Flex vertical gap="middle">
      {!provider.isEnabled && (
        <Alert
          type="warning"
          showIcon
          title="This provider is disabled."
          description="Disabled providers are hidden from the login page and reject token exchange attempts."
        />
      )}

      <Row gutter={[16, 16]}>
        <Col xs={24} lg={14}>
          <Descriptions column={1} size="small">
            <Item label="Name">{provider.name}</Item>
            <Item label="Display Name">{provider.displayName}</Item>
            <Item label="Type">
              {providerTypeLabel(provider.providerType)}
            </Item>
            <Item label="Authority">
              <Text copyable>{provider.authority}</Text>
            </Item>
            <Item label="Client ID">
              <Text copyable>{provider.clientId}</Text>
            </Item>
            <Item label="Audience">
              <Text copyable>{provider.audience}</Text>
            </Item>
            <Item label="Scopes">
              <ul style={{ margin: 0, paddingInlineStart: 20 }}>
                {(provider.scopes ?? []).map((scope) => (
                  <li key={scope}>{scope}</li>
                ))}
              </ul>
            </Item>
            {isMicrosoftEntraId && (
              <Item label="Allowed Tenant IDs">
                <ul style={{ margin: 0, paddingInlineStart: 20 }}>
                  {(provider.allowedTenantIds ?? []).map((tenantId) => (
                    <li key={tenantId}>{tenantId}</li>
                  ))}
                </ul>
              </Item>
            )}
            <Item label="Clock Skew">
              {provider.clockSkewSeconds} seconds
            </Item>
          </Descriptions>
        </Col>

        <Col xs={24} lg={10}>
          <Flex vertical gap="middle">
            <DiscoveryTest providerId={provider.id} />

            <Card size="small" title="Registration Policy">
              <Descriptions column={1} size="small">
                <Item label="Automatic user registration">
                  {provider.allowAutoRegistration ? 'Allowed' : 'Disabled'}
                </Item>
                {provider.allowAutoRegistration && (
                  <>
                    <Item label="Require matching employee record">
                      {provider.requireEmployeeRecord ? 'Yes' : 'No'}
                    </Item>
                    <Item label="Default role for new users">
                      {defaultRoleName}
                    </Item>
                  </>
                )}
              </Descriptions>
            </Card>
          </Flex>
        </Col>
      </Row>
    </Flex>
  )
}

export default OidcProviderDetails
