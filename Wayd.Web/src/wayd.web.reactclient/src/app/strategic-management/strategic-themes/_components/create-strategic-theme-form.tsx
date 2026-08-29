'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import { CreateStrategicThemeRequest } from '@/src/services/wayd-api'
import { useCreateStrategicThemeMutation } from '@/src/store/features/strategic-management/strategic-themes-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface CreateStrategicThemeFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CreateStrategicThemeFormValues {
  name: string
  description: string
}

const mapToRequestValues = (
  values: CreateStrategicThemeFormValues,
): CreateStrategicThemeRequest => {
  return {
    name: values.name,
    description: values.description,
  } as CreateStrategicThemeRequest
}

const CreateStrategicThemeForm = ({
  onFormComplete,
  onFormCancel,
}: CreateStrategicThemeFormProps) => {
  const messageApi = useMessage()

  const [createStrategicTheme] = useCreateStrategicThemeMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateStrategicThemeFormValues>({
      onSubmit: async (values: CreateStrategicThemeFormValues, form) => {
          try {
            const request = mapToRequestValues(values)
            const response = await createStrategicTheme(request)
            if (response.error) {
              throw response.error
            }
            messageApi.success(
              'Strategic Theme created successfully. Strategic Theme key: ' +
                response.data!.key,
            )
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
                  'An error occurred while creating the strategic theme. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while creating the strategic theme. Please try again.',
      permission: 'Permissions.StrategicThemes.Create',
    })

  return (
    <Modal
      title="Create Strategic Theme"
      open={isOpen}
      width={'60vw'}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="create-strategic-theme-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[
            { required: true, message: 'Name is required' },
            { max: 64 },
          ]}
        >
          <Input maxLength={64} showCount />
        </Item>
        <Item
          name="description"
          label="Description"
          rules={[
            { required: true, message: 'Description is required' },
            { max: 1024 },
          ]}
        >
          <MarkdownEditor
            value={form.getFieldValue('description')}
            onChange={(value) =>
              form.setFieldValue('description', value || '')
            }
            maxLength={1024}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default CreateStrategicThemeForm
