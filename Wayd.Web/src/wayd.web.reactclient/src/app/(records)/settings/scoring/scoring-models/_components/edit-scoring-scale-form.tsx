'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  ScoringScaleDto,
  ScoringScaleRequest,
} from '@/src/services/wayd-api'
import { useUpdateScoringScaleMutation } from '@/src/store/features/scoring/scoring-models-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useEffect } from 'react'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface EditScoringScaleFormProps {
  scoringModelId: string
  scale: ScoringScaleDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditScoringScaleFormValues {
  name: string
}

const EditScoringScaleForm = ({
  scoringModelId,
  scale,
  onFormComplete,
  onFormCancel,
}: EditScoringScaleFormProps) => {
  const messageApi = useMessage()

  const [updateScoringScale] = useUpdateScoringScaleMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditScoringScaleFormValues>({
      onSubmit: async (values: EditScoringScaleFormValues, form) => {
        try {
          const response = await updateScoringScale({
            scoringModelId,
            scaleId: scale.id,
            name: values.name,
          } as { scoringModelId: string; scaleId: string } & ScoringScaleRequest)
          if (response.error) {
            throw response.error
          }
          messageApi.success('Scale updated successfully.')
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
                'An error occurred while updating the scale. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the scale. Please try again.',
      permission: 'Permissions.ScoringModels.Update',
    })

  useEffect(() => {
    if (!scale) return
    form.setFieldsValue({ name: scale.name })
  }, [scale, form])

  return (
    <Modal
      title="Edit Scale"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form form={form} size="small" layout="vertical" name="update-scoring-scale-form">
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 64 }]}
        >
          <Input showCount maxLength={64} />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditScoringScaleForm
