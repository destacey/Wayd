'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { UpdateScoringModelRequest } from '@/src/services/wayd-api'
import {
  useGetScoringModelQuery,
  useUpdateScoringModelMutation,
} from '@/src/store/features/scoring/scoring-models-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Modal } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import { useEffect } from 'react'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface EditScoringModelFormProps {
  scoringModelId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface UpdateScoringModelFormValues {
  name: string
  description: string
}

const mapToRequestValues = (
  values: UpdateScoringModelFormValues,
): UpdateScoringModelRequest => {
  return {
    name: values.name,
    description: values.description,
  } as UpdateScoringModelRequest
}

const EditScoringModelForm = ({
  scoringModelId,
  onFormComplete,
  onFormCancel,
}: EditScoringModelFormProps) => {
  const messageApi = useMessage()

  const { data: scoringModelData, error } =
    useGetScoringModelQuery(scoringModelId)

  const [updateScoringModel] = useUpdateScoringModelMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<UpdateScoringModelFormValues>({
      onSubmit: async (values: UpdateScoringModelFormValues, form) => {
        try {
          const request = mapToRequestValues(values)
          const response = await updateScoringModel({
            id: scoringModelData!.id,
            ...request,
          })
          if (response.error) {
            throw response.error
          }
          messageApi.success('Scoring Model updated successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            const formErrors = toFormErrors(apiError.errors)
            form.setFields(formErrors)
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              (isApiError(apiError) ? apiError.detail : undefined) ??
                'An error occurred while updating the scoring model. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the scoring model. Please try again.',
      permission: 'Permissions.ScoringModels.Update',
    })

  useEffect(() => {
    if (!scoringModelData) return
    form.setFieldsValue({
      name: scoringModelData.name,
      description: scoringModelData.description,
    })
  }, [scoringModelData, form])

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading the scoring model. Please try again.',
      )
    }
  }, [error, messageApi])

  return (
    <Modal
      title="Edit Scoring Model"
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
        name="update-scoring-model-form"
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

export default EditScoringModelForm
