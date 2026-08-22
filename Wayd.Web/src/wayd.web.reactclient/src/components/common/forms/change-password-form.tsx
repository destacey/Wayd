'use client'

import { Alert, Form, Input, Modal } from 'antd'
import { useChangePasswordMutation } from '@/src/store/features/user-management/profile-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import useAuth from '@/src/components/contexts/auth'

const { Item } = Form

export interface ChangePasswordFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
  /** When true, the user must change their password. Cancel will sign them out. */
  required?: boolean
}

interface ChangePasswordFormValues {
  currentPassword: string
  newPassword: string
  confirmPassword: string
}

const ChangePasswordForm = ({
  onFormComplete,
  onFormCancel,
  required = false,
}: ChangePasswordFormProps) => {
  const messageApi = useMessage()
  const { logout } = useAuth()
  const [changePassword] = useChangePasswordMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ChangePasswordFormValues>({
      onSubmit: async (values: ChangePasswordFormValues) => {
        try {
          const response = await changePassword({
            currentPassword: values.currentPassword,
            newPassword: values.newPassword,
          })

          if (response.error) {
            throw response.error
          }

          messageApi.success('Password changed successfully. You will be logged out.')
          // Log out after a brief delay so the user sees the success message
          setTimeout(() => logout(), 1500)
          return true
        } catch (error: any) {
          messageApi.error(
            error.data?.detail ??
              error.detail ??
              'Failed to change password.',
          )
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'An unexpected error occurred while changing the password.',
    })

  return (
    <Modal
      title="Change Password"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Change Password"
      confirmLoading={isSaving}
      onCancel={required ? () => logout() : handleCancel}
      cancelText={required ? 'Sign Out' : 'Cancel'}
      closable={!required}
      mask={{ closable: !required }}
      keyboard={false}
      destroyOnHidden
    >
      <Alert
        title={
          required
            ? 'You must change your password before continuing. You can also sign out if you prefer.'
            : 'You will be logged out after changing your password and will need to sign in again with your new password.'
        }
        type={required ? 'warning' : 'info'}
        showIcon
        style={{ marginBottom: 16 }}
      />
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="change-password-form"
      >
        <Item
          label="Current Password"
          name="currentPassword"
          rules={[
            { required: true, message: 'Please enter your current password.' },
          ]}
        >
          <Input.Password />
        </Item>
        <Item
          label="New Password"
          name="newPassword"
          dependencies={['currentPassword']}
          rules={[
            { required: true, message: 'Please enter a new password.' },
            { min: 8, message: 'Password must be at least 8 characters.' },
            { pattern: /[A-Z]/, message: 'Password must contain at least one uppercase letter.' },
            { pattern: /[a-z]/, message: 'Password must contain at least one lowercase letter.' },
            { pattern: /[0-9]/, message: 'Password must contain at least one digit.' },
            ({ getFieldValue }) => ({
              validator(_, value) {
                if (!value || getFieldValue('currentPassword') !== value) {
                  return Promise.resolve()
                }
                return Promise.reject(
                  new Error(
                    'New password must be different from current password.',
                  ),
                )
              },
            }),
          ]}
        >
          <Input.Password />
        </Item>
        <Item
          label="Confirm New Password"
          name="confirmPassword"
          dependencies={['newPassword']}
          rules={[
            { required: true, message: 'Please confirm your new password.' },
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

export default ChangePasswordForm
