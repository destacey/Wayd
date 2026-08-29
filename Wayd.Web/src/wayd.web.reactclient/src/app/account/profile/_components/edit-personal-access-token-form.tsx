'use client'

import { Form, Input, Modal, DatePicker } from 'antd'
import { useEffect } from 'react'
import { useMessage } from '@/src/components/contexts/messaging'
import dayjs, { Dayjs } from 'dayjs'
import { useUpdatePersonalAccessTokenMutation } from '@/src/store/features/user-management/personal-access-tokens-api'
import { PersonalAccessTokenDto } from '@/src/services/wayd-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface EditPersonalAccessTokenFormProps {
  token: PersonalAccessTokenDto
  onFormUpdate: () => void
  onFormCancel: () => void
}

interface EditTokenFormValues {
  name: string
  expiresAt: Dayjs
}

const EditPersonalAccessTokenForm = ({
  token,
  onFormUpdate,
  onFormCancel,
}: EditPersonalAccessTokenFormProps) => {
  const messageApi = useMessage()

  const [updateToken] = useUpdatePersonalAccessTokenMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditTokenFormValues>({
      onSubmit: async (values: EditTokenFormValues, form) => {
        try {
          const response = await updateToken({
            id: token.id!,
            request: {
              name: values.name,
              expiresAt: values.expiresAt.toDate(),
            },
          })

          if (response.error) {
            throw response.error
          }

          messageApi.success('Personal access token updated successfully')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            const formErrors = toFormErrors(apiError.errors)
            form.setFields(formErrors)
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while updating the PAT. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormUpdate,
      onCancel: onFormCancel,
    })

  useEffect(() => {
    if (!token || !isOpen) return
    form.setFieldsValue({
      name: token.name!,
      expiresAt: dayjs(token.expiresAt),
    })
  }, [token, isOpen, form])

  return (
    <Modal
      title="Edit Personal Access Token"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
      width={600}
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="edit-personal-access-token-form"
      >
        <Item
          label="Token Name"
          name="name"
          rules={[
            { required: true, message: 'Please enter a name for this token' },
            { max: 100, message: 'Name must be 100 characters or less' },
          ]}
        >
          <Input placeholder="e.g., CI/CD Pipeline, Personal Script" />
        </Item>

        <Item
          name="expiresAt"
          label="Expiration Date"
          help="Maximum: 2 years from now."
          rules={[
            {
              required: true,
              message: 'Please enter an expiration date for this token',
            },
          ]}
        >
          <DatePicker
            showTime
            disabledDate={(current) => {
              const minDate = dayjs()
              const maxDate = dayjs().add(2, 'year')
              return current && (current < minDate || current > maxDate)
            }}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditPersonalAccessTokenForm
