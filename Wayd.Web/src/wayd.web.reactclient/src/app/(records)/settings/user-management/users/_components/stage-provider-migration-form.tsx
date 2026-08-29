'use client'

import { Alert, Form, Modal, Select, Typography } from 'antd'
import { toFormErrors } from '@/src/utils'
import { useStageProviderMigrationMutation } from '@/src/store/features/user-management/users-api'
import { useGetAuthProvidersQuery } from '@/src/store/features/common/auth-providers-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { Text } = Typography

export interface StageProviderMigrationFormProps {
  userId: string
  userName: string
  currentLoginProvider: string
  currentPendingProviderId?: string | null
  onFormComplete: () => void
  onFormCancel: () => void
}

interface StageProviderMigrationFormValues {
  targetProviderId: string
}

const StageProviderMigrationForm = ({
  userId,
  userName,
  currentLoginProvider,
  currentPendingProviderId,
  onFormComplete,
  onFormCancel,
}: StageProviderMigrationFormProps) => {
  const messageApi = useMessage()
  const [stageProviderMigration] = useStageProviderMigrationMutation()
  const { data: authProviders, isLoading: providersLoading } =
    useGetAuthProvidersQuery()

  // Public endpoint returns only enabled providers; filter to those that differ
  // from the user's current provider. 'Wayd' is never in the OIDC list.
  const eligibleProviders = (authProviders?.oidc ?? []).filter(
    (p) => p.name !== currentLoginProvider,
  )

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<StageProviderMigrationFormValues>({
      onSubmit: async (values, form) => {
        try {
          const response = await stageProviderMigration({
            userId,
            targetProviderId: values.targetProviderId,
          })

          if (response.error) {
            throw response.error
          }

          messageApi.success(
            'Provider migration staged. Will complete on the user’s next sign-in via the target provider.',
          )
          return true
        } catch (error: any) {
          if (error.status === 422 && error.errors) {
            const formErrors = toFormErrors(error.errors)
            form.setFields(formErrors)
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              error.data?.detail ??
                error.detail ??
                'An unexpected error occurred while staging the migration.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An unexpected error occurred while staging the migration.',
      permission: 'Permissions.Users.Update',
    })

  return (
    <Modal
      title={`Change Identity Provider — ${userName}`}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText={
        currentPendingProviderId ? 'Replace Pending Migration' : 'Stage Migration'
      }
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="stage-provider-migration-form"
      >
        {currentPendingProviderId && (
          <Alert
            type="warning"
            showIcon
            title="Migration already pending"
            description={
              <>
                A migration is already staged to provider{' '}
                <Text code>{currentPendingProviderId}</Text>. Submitting will
                replace it.
              </>
            }
            style={{ marginBottom: 16 }}
          />
        )}
        <Alert
          type="info"
          showIcon
          description="The rebind happens automatically the next time the user signs in via the target provider. Their UserId, permissions, and links to other records are preserved."
          style={{ marginBottom: 16 }}
        />
        <Item
          label="Target Identity Provider"
          name="targetProviderId"
          rules={[
            {
              required: true,
              message: 'Please select a target provider.',
            },
          ]}
        >
          <Select
            loading={providersLoading}
            placeholder="Select a provider"
            options={eligibleProviders.map((p) => ({
              label: p.displayName,
              value: p.name,
            }))}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default StageProviderMigrationForm
