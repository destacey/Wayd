'use client'

import { Form, Input, Modal, DatePicker, Alert } from 'antd'
import { useMessage } from '@/src/components/contexts/messaging'
import dayjs, { Dayjs } from 'dayjs'
import { ExclamationCircleOutlined } from '@ant-design/icons'
import { useCreatePersonalAccessTokenMutation } from '@/src/store/features/user-management/personal-access-tokens-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface CreatePersonalAccessTokenFormProps {
  onFormCreate: (token: string) => void
  onFormCancel: () => void
}

interface CreateTokenFormValues {
  name: string
  expiresAt: Dayjs
}

const CreatePersonalAccessTokenForm = ({
  onFormCreate,
  onFormCancel,
}: CreatePersonalAccessTokenFormProps) => {
  const messageApi = useMessage()

  const [createToken] = useCreatePersonalAccessTokenMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateTokenFormValues>({
      onSubmit: async (values: CreateTokenFormValues, form) => {
        try {
          const response = await createToken({
            name: values.name,
            expiresAt: values.expiresAt.toDate(),
          })

          if (response.error) {
            throw response.error
          }

          messageApi.success('Personal access token created successfully')
          onFormCreate(response.data!.token!)
          return false
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            const formErrors = toFormErrors(apiError.errors)
            form.setFields(formErrors)
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while creating the PAT. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: () => {},
      onCancel: onFormCancel,
    })

  return (
    <Modal
      title="Create Personal Access Token"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
      width={600}
    >
      <Alert
        title="Important"
        description="After creation, the token will only be shown once. Make sure to copy it to a secure location."
        type="warning"
        showIcon
        icon={<ExclamationCircleOutlined />}
        style={{ marginBottom: 16 }}
      />
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="create-personal-access-token-form"
        initialValues={{ expiresAt: dayjs().add(1, 'year') }}
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

export default CreatePersonalAccessTokenForm
