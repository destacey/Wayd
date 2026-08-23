'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { ScoringScaleRequest } from '@/src/services/wayd-api'
import { useAddScoringScaleMutation } from '@/src/store/features/scoring/scoring-models-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface AddScoringScaleFormProps {
  scoringModelId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface AddScoringScaleFormValues {
  name: string
}

const AddScoringScaleForm = ({
  scoringModelId,
  onFormComplete,
  onFormCancel,
}: AddScoringScaleFormProps) => {
  const messageApi = useMessage()

  const [addScoringScale] = useAddScoringScaleMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<AddScoringScaleFormValues>({
      onSubmit: async (values: AddScoringScaleFormValues, form) => {
        try {
          const response = await addScoringScale({
            scoringModelId,
            name: values.name,
          } as { scoringModelId: string } & ScoringScaleRequest)
          if (response.error) {
            throw response.error
          }
          messageApi.success('Scale added successfully.')
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
                'An error occurred while adding the scale. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while adding the scale. Please try again.',
      permission: 'Permissions.ScoringModels.Update',
    })

  return (
    <Modal
      title="Add Scale"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Add"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form form={form} size="small" layout="vertical" name="add-scoring-scale-form">
        <Item
          label="Name"
          name="name"
          tooltip="A reusable scale (e.g., Fibonacci, Impact) that criteria can be rated against."
          rules={[{ required: true, message: 'Name is required' }, { max: 64 }]}
        >
          <Input showCount maxLength={64} />
        </Item>
      </Form>
    </Modal>
  )
}

export default AddScoringScaleForm
