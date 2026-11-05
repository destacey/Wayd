'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  EstimationScaleValueRequest,
  UpdateEstimationScaleRequest,
} from '@/src/services/wayd-api'
import {
  useGetEstimationScaleQuery,
  useUpdateEstimationScaleMutation,
} from '@/src/store/features/planning/estimation-scales-api'
import { toFormErrors, toApiError } from '@/src/utils'
import { Form, Modal } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import { useEffect } from 'react'
import { useModalForm } from '@/src/hooks'
import EstimationScaleValuesField, {
  estimationScaleValueErrorFieldFor,
} from './estimation-scale-values-field'

const { Item } = Form

export interface EditEstimationScaleFormProps {
  estimationScaleId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditEstimationScaleFormValues {
  name: string
  description?: string
  values: EstimationScaleValueRequest[]
}

const EditEstimationScaleForm = ({
  estimationScaleId,
  onFormComplete,
  onFormCancel,
}: EditEstimationScaleFormProps) => {
  const messageApi = useMessage()

  const { data: scaleData, isLoading: scaleLoading } =
    useGetEstimationScaleQuery(estimationScaleId)
  const [updateEstimationScale] = useUpdateEstimationScaleMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditEstimationScaleFormValues>({
      onSubmit: async (values: EditEstimationScaleFormValues, form) => {
          try {
            const request: UpdateEstimationScaleRequest = {
              id: estimationScaleId,
              name: values.name,
              description: values.description,
              values: values.values,
            }
            const response = await updateEstimationScale(request)
            if (response.error) {
              throw response.error
            }
            messageApi.success('Estimation scale updated successfully.')
            return true
          } catch (error) {
            const apiError = toApiError(error)
            if (apiError.status === 422 && apiError.errors) {
              const formErrors = toFormErrors(apiError.errors, {
                fieldFor: estimationScaleValueErrorFieldFor,
              })
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                apiError.detail ??
                  'An error occurred while updating the estimation scale. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the estimation scale. Please try again.',
      permission: 'Permissions.EstimationScales.Update',
    })

  useEffect(() => {
    if (!scaleData) return
    form.setFieldsValue({
      name: scaleData.name,
      description: scaleData.description,
      values: scaleData.currentVersion.values.map((v) => ({
        value: v.value,
        isEstimate: v.isEstimate,
      })),
    })
  }, [scaleData, form])

  const versionNumber = scaleData?.currentVersion.number

  return (
    <Modal
      title="Edit Estimation Scale"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      loading={scaleLoading}
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
        name="edit-estimation-scale-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[
            { required: true, message: 'Name is required' },
            { max: 128 },
          ]}
        >
          <TextArea
            autoSize={{ minRows: 1, maxRows: 2 }}
            showCount
            maxLength={128}
          />
        </Item>
        <Item name="description" label="Description" rules={[{ max: 1024 }]}>
          <TextArea
            autoSize={{ minRows: 3, maxRows: 6 }}
            showCount
            maxLength={1024}
          />
        </Item>
        <EstimationScaleValuesField
          extra={
            versionNumber &&
            `Changing the values publishes version ${versionNumber + 1}. Sessions already on version ${versionNumber} keep voting on it.`
          }
        />
      </Form>
    </Modal>
  )
}

export default EditEstimationScaleForm
