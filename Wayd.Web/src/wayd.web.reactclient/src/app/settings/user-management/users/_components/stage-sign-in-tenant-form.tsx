'use client'

import { Alert, Form, Modal, Select, Typography } from 'antd'
import { toFormErrors } from '@/src/utils'
import {
  useGetEntraTenantIdsQuery,
  useStageSignInTenantMutation,
} from '@/src/store/features/user-management/users-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { Text } = Typography

export interface StageSignInTenantFormProps {
  userId: string
  userName: string
  currentTenantId?: string | null
  onFormComplete: () => void
  onFormCancel: () => void
}

interface StageSignInTenantFormValues {
  tenantId?: string
}

const StageSignInTenantForm = ({
  userId,
  userName,
  currentTenantId,
  onFormComplete,
  onFormCancel,
}: StageSignInTenantFormProps) => {
  const messageApi = useMessage()
  const [stageSignInTenant] = useStageSignInTenantMutation()
  const { data: tenantIds, isLoading: tenantsLoading } =
    useGetEntraTenantIdsQuery()

  // With a single allowed tenant the API uses it, so there is nothing to choose.
  const mustChoose = (tenantIds?.length ?? 0) > 1

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<StageSignInTenantFormValues>({
      onSubmit: async (values, form) => {
        try {
          const response = await stageSignInTenant({
            userId,
            tenantId: mustChoose ? values.tenantId : undefined,
          })

          if (response.error) {
            throw response.error
          }

          messageApi.success(
            'Sign-in tenant set. Their next sign-in from it links the account.',
          )
          return true
        } catch (error: any) {
          if (error.status === 422 && error.errors) {
            form.setFields(toFormErrors(error.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              error.data?.detail ??
                error.detail ??
                'An unexpected error occurred while setting the sign-in tenant.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An unexpected error occurred while setting the sign-in tenant.',
      permission: 'Permissions.Users.Update',
    })

  return (
    <Modal
      title={`Set Sign-in Tenant — ${userName}`}
      open={isOpen}
      onOk={handleOk}
      // Until the tenants load there is nothing to pick, so submitting would send no
      // tenant — which a multi-tenant provider rejects.
      okButtonProps={{
        disabled: !isValid || tenantsLoading || tenantIds?.length === 0,
      }}
      okText="Set Tenant"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="stage-sign-in-tenant-form"
        initialValues={{ tenantId: currentTenantId ?? undefined }}
      >
        <Alert
          type="info"
          showIcon
          description="Their next sign-in from this tenant, with a matching email, links the account. A sign-in from any other tenant is refused."
          style={{ marginBottom: 16 }}
        />
        {tenantIds?.length === 0 && (
          <Alert
            type="warning"
            showIcon
            title="Microsoft Entra ID isn't configured. Add it under Settings → Identity Providers first."
            style={{ marginBottom: 16 }}
          />
        )}
        {tenantIds?.length === 1 && (
          <Item label="Sign-in Tenant">
            <Text code>{tenantIds[0]}</Text>
          </Item>
        )}
        {mustChoose && (
          <Item
            label="Sign-in Tenant"
            name="tenantId"
            rules={[{ required: true, message: 'Sign-in tenant is required' }]}
          >
            <Select
              loading={tenantsLoading}
              placeholder="Select a tenant"
              options={tenantIds!.map((id) => ({ value: id, label: id }))}
            />
          </Item>
        )}
      </Form>
    </Modal>
  )
}

export default StageSignInTenantForm
