'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  CreateOidcProviderRequest,
  OidcProviderType,
} from '@/src/services/wayd-api'
import { useCreateOidcProviderMutation } from '@/src/store/features/user-management/oidc-providers-api'
import { useGetRolesQuery } from '@/src/store/features/user-management/roles-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { useModalForm } from '@/src/hooks'
import {
  Alert,
  Form,
  Input,
  InputNumber,
  Modal,
  Select,
  Switch,
  Typography,
} from 'antd'
import { useState } from 'react'
import TagInput from './tag-input'

const { Item } = Form

export interface CreateOidcProviderFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CreateOidcProviderFormValues {
  name: string
  displayName: string
  providerType: OidcProviderType
  authority: string
  clientId: string
  audience: string
  scopes: string[]
  allowedTenantIds?: string[]
  clockSkewSeconds: number
  isEnabled: boolean
  allowAutoRegistration: boolean
  requireEmployeeRecord: boolean
  defaultRoleId?: string
}

const CreateOidcProviderForm = ({
  onFormComplete,
  onFormCancel,
}: CreateOidcProviderFormProps) => {
  const messageApi = useMessage()
  const { data: roles } = useGetRolesQuery()
  const [createOidcProvider] = useCreateOidcProviderMutation()
  const [providerType, setProviderType] = useState<OidcProviderType>(
    OidcProviderType.GenericOidc,
  )

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateOidcProviderFormValues>({
      onSubmit: async (values, form) => {
        try {
          const request: CreateOidcProviderRequest = {
            name: values.name,
            displayName: values.displayName,
            providerType: values.providerType,
            authority: values.authority,
            clientId: values.clientId,
            audience: values.audience,
            scopes: values.scopes ?? [],
            allowedTenantIds:
              values.providerType === OidcProviderType.MicrosoftEntraId
                ? (values.allowedTenantIds ?? [])
                : undefined,
            clockSkewSeconds: values.clockSkewSeconds ?? 60,
            isEnabled: values.isEnabled ?? true,
            allowAutoRegistration: values.allowAutoRegistration ?? true,
            requireEmployeeRecord: values.allowAutoRegistration
              ? (values.requireEmployeeRecord ?? true)
              : true,
            defaultRoleId: values.allowAutoRegistration
              ? (values.defaultRoleId ?? undefined)
              : undefined,
          }
          const response = await createOidcProvider(request)
          if (response.error) throw response.error
          messageApi.success('Identity provider created successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while creating the identity provider.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      permission: 'Permissions.OidcProviders.Create',
    })

  const allowAutoRegistration = Form.useWatch('allowAutoRegistration', form)

  return (
    <Modal
      title="Create Identity Provider"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
      width={640}
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="create-oidc-provider-form"
        initialValues={{
          providerType: OidcProviderType.GenericOidc,
          scopes: ['openid', 'profile', 'email'],
          clockSkewSeconds: 60,
          isEnabled: true,
          allowAutoRegistration: false,
          requireEmployeeRecord: true,
        }}
      >
        <Item
          label="Name"
          name="name"
          rules={[
            { required: true, message: 'Name is required' },
            {
              pattern: /^[a-z0-9-]+$/,
              message: 'Only lowercase letters, digits, and hyphens',
            },
            { max: 50 },
          ]}
          tooltip="Immutable identifier used in URLs and configuration. Only lowercase letters, digits, and hyphens."
        >
          <Input maxLength={50} />
        </Item>

        <Item
          label="Display Name"
          name="displayName"
          rules={[
            { required: true, message: 'Display name is required' },
            { max: 100 },
          ]}
          tooltip="Label shown on the login page button."
        >
          <Input maxLength={100} />
        </Item>

        <Item
          label="Provider Type"
          name="providerType"
          rules={[{ required: true, message: 'Provider type is required' }]}
          tooltip="Microsoft Entra ID enforces a tenant allowlist. Generic OIDC accepts any token from the configured issuer. Immutable after creation."
        >
          <Select
            options={[
              { value: OidcProviderType.GenericOidc, label: 'Generic OIDC' },
              {
                value: OidcProviderType.MicrosoftEntraId,
                label: 'Microsoft Entra ID',
              },
            ]}
            onChange={(val) => setProviderType(val)}
          />
        </Item>

        <Item
          label="Authority"
          name="authority"
          rules={[
            { required: true, message: 'Authority URL is required' },
            { type: 'url', message: 'Must be a valid URL' },
            { max: 500 },
          ]}
          tooltip="The base URL of the OIDC issuer, e.g. https://login.microsoftonline.com/{tenant}/v2.0"
        >
          <Input maxLength={500} placeholder="https://..." />
        </Item>

        <Item
          label="Client ID"
          name="clientId"
          rules={[
            { required: true, message: 'Client ID is required' },
            { max: 200 },
          ]}
          tooltip="The public OAuth client ID used by the browser when initiating sign-in. For Entra ID this is the SPA (client) app registration's client ID."
        >
          <Input maxLength={200} />
        </Item>

        <Item
          label="Audience"
          name="audience"
          rules={[
            { required: true, message: 'Audience is required' },
            { max: 500 },
          ]}
          tooltip="Expected audience (aud) claim in the token. For Entra ID this is the bare API client ID GUID."
        >
          <Input maxLength={500} />
        </Item>

        <Item
          label="Scopes"
          name="scopes"
          tooltip="OAuth scopes the browser requests when initiating sign-in. For Entra ID include the API scope (e.g. api://{api-client-id}/access_as_user) so Entra issues an access token with the correct audience."
        >
          <TagInput placeholder="Add scope and press Enter" />
        </Item>

        {providerType === OidcProviderType.MicrosoftEntraId && (
          <Item
            label="Allowed Tenant IDs"
            name="allowedTenantIds"
            rules={[
              {
                validator: (_, value) =>
                  !value || value.length === 0
                    ? Promise.reject('At least one tenant ID is required')
                    : Promise.resolve(),
              },
            ]}
            tooltip="GUID tenant IDs that are permitted to authenticate. Add at least one."
          >
            <TagInput placeholder="Add tenant ID and press Enter" />
          </Item>
        )}

        <Item
          label="Clock Skew (seconds)"
          name="clockSkewSeconds"
          rules={[{ type: 'number', min: 0, max: 600 }]}
          tooltip="Tolerance for token expiry/not-before checks to account for clock drift between servers. 60 seconds is the standard default."
        >
          <InputNumber min={0} max={600} style={{ width: '100%' }} />
        </Item>

        <Item
          label="Enabled"
          name="isEnabled"
          valuePropName="checked"
          tooltip="Disabled providers are hidden from the login page and reject token exchange attempts."
        >
          <Switch />
        </Item>

        <Typography.Title level={5} style={{ marginTop: 8 }}>
          Registration
        </Typography.Title>

        <Item
          label="Allow automatic user registration"
          name="allowAutoRegistration"
          valuePropName="checked"
          tooltip="When on, a user signing in through this provider for the first time has an account created automatically. When off, unknown users are rejected and an admin must create their account first."
        >
          <Switch />
        </Item>

        {allowAutoRegistration && (
          <>
            <Item
              label="Require matching employee record"
              name="requireEmployeeRecord"
              valuePropName="checked"
              tooltip="When on, only users whose email matches an existing employee can self-register."
            >
              <Switch />
            </Item>

            <Item noStyle shouldUpdate>
              {({ getFieldValue }) =>
                getFieldValue('requireEmployeeRecord') ? null : (
                  <Alert
                    type="warning"
                    showIcon
                    style={{ marginBottom: 16 }}
                    title="Anyone who can authenticate through this provider will be granted an account."
                  />
                )
              }
            </Item>

            <Item
              label="Default role for new users"
              name="defaultRoleId"
              rules={[{ required: true, message: 'A default role is required' }]}
              tooltip="Role assigned to users created automatically through this provider."
            >
              <Select
                placeholder="Select a role"
                options={(roles ?? []).map((r) => ({
                  value: r.id,
                  label: r.name,
                }))}
              />
            </Item>
          </>
        )}
      </Form>
    </Modal>
  )
}

export default CreateOidcProviderForm
