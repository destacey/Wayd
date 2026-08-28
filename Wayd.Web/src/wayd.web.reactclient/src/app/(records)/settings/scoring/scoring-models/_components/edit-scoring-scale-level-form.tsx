'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  ScoringRatingLevelDto,
  ScoringScaleLevelRequest,
} from '@/src/services/wayd-api'
import { useUpdateScoringScaleLevelMutation } from '@/src/store/features/scoring/scoring-models-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, InputNumber, Modal } from 'antd'
import { useEffect } from 'react'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface EditScoringScaleLevelFormProps {
  scoringModelId: string
  scaleId: string
  level: ScoringRatingLevelDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditScoringScaleLevelFormValues {
  label: string
  value: number
}

const EditScoringScaleLevelForm = ({
  scoringModelId,
  scaleId,
  level,
  onFormComplete,
  onFormCancel,
}: EditScoringScaleLevelFormProps) => {
  const messageApi = useMessage()

  const [updateScoringScaleLevel] = useUpdateScoringScaleLevelMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditScoringScaleLevelFormValues>({
      onSubmit: async (values: EditScoringScaleLevelFormValues, form) => {
        try {
          const response = await updateScoringScaleLevel({
            scoringModelId,
            scaleId,
            levelId: level.id,
            label: values.label,
            value: values.value,
          } as {
            scoringModelId: string
            scaleId: string
            levelId: string
          } & ScoringScaleLevelRequest)
          if (response.error) {
            throw response.error
          }
          messageApi.success('Rating level updated successfully.')
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
                'An error occurred while updating the rating level. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the rating level. Please try again.',
      permission: 'Permissions.ScoringModels.Update',
    })

  useEffect(() => {
    if (!level) return
    form.setFieldsValue({ label: level.label, value: level.value })
  }, [level, form])

  return (
    <Modal
      title="Edit Rating Level"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="update-scoring-scale-level-form"
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

export default EditScoringScaleLevelForm
