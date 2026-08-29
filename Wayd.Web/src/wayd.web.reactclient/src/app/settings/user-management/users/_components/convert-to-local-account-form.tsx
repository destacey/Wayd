'use client'

import { Alert, Form, Input, Modal } from 'antd'
import { toFormErrors } from '@/src/utils'
import { useConvertToLocalAccountMutation } from '@/src/store/features/user-management/users-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface ConvertToLocalAccountFormProps {
  userId: string
  userName: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ConvertToLocalAccountFormValues {
  newPassword: string
  confirmPassword: string
}

const ConvertToLocalAccountForm = ({
  userId,
  userName,
  onFormComplete,
  onFormCancel,
}: ConvertToLocalAccountFormProps) => {
  const messageApi = useMessage()
  const [convertToLocal] = useConvertToLocalAccountMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ConvertToLocalAccountFormValues>({
      onSubmit: async (values, form) => {
        try {
          const response = await convertToLocal({
            userId,
            newPassword: values.newPassword,
          })

          if (response.error) {
            throw response.error
          }

          messageApi.success(
            'User converted to a local account. They must change their password on next login.',
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
                'An unexpected error occurred.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'An unexpected error occurred.',
      permission: 'Permissions.Users.Update',
    })

  return (
    <Modal
      title={`Convert to Local Account — ${userName}`}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid, danger: true }}
      okText="Convert to Local Account"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="convert-to-local-account-form"
      >
        <Alert
          type="warning"
          showIcon
          description="This immediately unlinks the user's external identity and switches them to password-based login. They will be required to change the password on their next sign-in."
          style={{ marginBottom: 16 }}
        />
        <Item
          label="Temporary Password"
          name="newPassword"
          rules={[
            { required: true, message: 'Please enter a temporary password.' },
            { min: 8, message: 'Password must be at least 8 characters.' },
          ]}
        >
          <Input.Password />
        </Item>
        <Item
          label="Confirm Password"
          name="confirmPassword"
          dependencies={['newPassword']}
          rules={[
            { required: true, message: 'Please confirm the password.' },
            ({ getFieldValue }) => ({
              validator(_, value) {
                if (!value || getFieldValue('newPassword') === value) {
                  return Promise.resolve()
                }
                return Promise.reject(new Error('Passwords do not match.'))
              },
            }),
          ]}
        >
          <Input.Password />
        </Item>
      </Form>
    </Modal>
  )
}

export default ConvertToLocalAccountForm
