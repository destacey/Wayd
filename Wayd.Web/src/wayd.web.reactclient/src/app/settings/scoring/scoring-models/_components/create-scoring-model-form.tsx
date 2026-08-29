'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { CreateScoringModelRequest } from '@/src/services/wayd-api'
import { useCreateScoringModelMutation } from '@/src/store/features/scoring/scoring-models-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Modal } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface CreateScoringModelFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CreateScoringModelFormValues {
  name: string
  description: string
}

const mapToRequestValues = (
  values: CreateScoringModelFormValues,
): CreateScoringModelRequest => {
  return {
    name: values.name,
    description: values.description,
  } as CreateScoringModelRequest
}

const CreateScoringModelForm = ({
  onFormComplete,
  onFormCancel,
}: CreateScoringModelFormProps) => {
  const messageApi = useMessage()

  const [createScoringModel] = useCreateScoringModelMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateScoringModelFormValues>({
      onSubmit: async (values: CreateScoringModelFormValues, form) => {
        try {
          const request = mapToRequestValues(values)
          const response = await createScoringModel(request)
          if (response.error) {
            throw response.error
          }
          messageApi.success(
            'Scoring Model created successfully. Key: ' + response.data,
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
                'An error occurred while creating the scoring model. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while creating the scoring model. Please try again.',
      permission: 'Permissions.ScoringModels.Create',
    })

  return (
    <Modal
      title="Create Scoring Model"
      open={isOpen}
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
        name="create-scoring-model-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 128 }]}
        >
          <TextArea
            autoSize={{ minRows: 1, maxRows: 2 }}
            showCount
            maxLength={128}
          />
        </Item>
        <Item
          name="description"
          label="Description"
          rules={[
            { required: true, message: 'Description is required' },
            { max: 1024 },
          ]}
        >
          <TextArea
            autoSize={{ minRows: 6, maxRows: 8 }}
            showCount
            maxLength={1024}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default CreateScoringModelForm
