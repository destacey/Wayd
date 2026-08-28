'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { ScoringScaleLevelRequest } from '@/src/services/wayd-api'
import { useAddScoringScaleLevelMutation } from '@/src/store/features/scoring/scoring-models-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, InputNumber, Modal } from 'antd'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface AddScoringScaleLevelFormProps {
  scoringModelId: string
  scaleId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface AddScoringScaleLevelFormValues {
  label: string
  value: number
}

const AddScoringScaleLevelForm = ({
  scoringModelId,
  scaleId,
  onFormComplete,
  onFormCancel,
}: AddScoringScaleLevelFormProps) => {
  const messageApi = useMessage()

  const [addScoringScaleLevel] = useAddScoringScaleLevelMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<AddScoringScaleLevelFormValues>({
      onSubmit: async (values: AddScoringScaleLevelFormValues, form) => {
        try {
          const response = await addScoringScaleLevel({
            scoringModelId,
            scaleId,
            label: values.label,
            value: values.value,
          } as {
            scoringModelId: string
            scaleId: string
          } & ScoringScaleLevelRequest)
          if (response.error) {
            throw response.error
          }
          messageApi.success('Rating level added successfully.')
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
                'An error occurred while adding the rating level. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while adding the rating level. Please try again.',
      permission: 'Permissions.ScoringModels.Update',
    })

  return (
    <Modal
      title="Add Rating Level"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Add"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="add-scoring-scale-level-form"
      >
        <Item
          label="Label"
          name="label"
          rules={[{ required: true, message: 'Label is required' }, { max: 64 }]}
        >
          <Input showCount maxLength={64} />
        </Item>
        <Item
          label="Value"
          name="value"
          rules={[{ required: true, message: 'Value is required' }]}
        >
          <InputNumber style={{ width: '100%' }} />
        </Item>
      </Form>
    </Modal>
  )
}

export default AddScoringScaleLevelForm
