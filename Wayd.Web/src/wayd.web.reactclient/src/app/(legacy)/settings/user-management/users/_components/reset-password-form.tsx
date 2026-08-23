'use client'

import { Form, Input, Modal } from 'antd'
import { toFormErrors } from '@/src/utils'
import { useResetUserPasswordMutation } from '@/src/store/features/user-management/users-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface ResetPasswordFormProps {
  userId: string
  userName: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface ResetPasswordFormValues {
  newPassword: string
  confirmPassword: string
}

const ResetPasswordForm = ({
  userId,
  userName,
  onFormComplete,
  onFormCancel,
}: ResetPasswordFormProps) => {
  const messageApi = useMessage()
  const [resetPassword] = useResetUserPasswordMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ResetPasswordFormValues>({
      onSubmit: async (values: ResetPasswordFormValues, form) => {
          try {
            const response = await resetPassword({
              userId,
              newPassword: values.newPassword,
            })

            if (response.error) {
              throw response.error
            }

            messageApi.success('Password reset successfully.')
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
                  'An unexpected error occurred while resetting the password.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An unexpected error occurred while resetting the password.',
      permission: 'Permissions.Users.Update',
    })

  return (
    <Modal
      title={`Reset Password — ${userName}`}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Reset Password"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="reset-password-form"
      >
        <Item
          label="New Password"
          name="newPassword"
          rules={[
            { required: true, message: 'Please enter a new password.' },
            { min: 8, message: 'Password must be at least 8 characters.' },
          ]}
        >
          <Input.Password />
        </Item>
        <Item
          label="Confirm New Password"
          name="confirmPassword"
          dependencies={['newPassword']}
          rules={[
            { required: true, message: 'Please confirm the new password.' },
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

export default ResetPasswordForm
