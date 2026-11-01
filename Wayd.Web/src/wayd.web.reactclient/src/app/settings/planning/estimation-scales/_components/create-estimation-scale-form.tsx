'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  CreateEstimationScaleRequest,
  EstimationScaleValueRequest,
} from '@/src/services/wayd-api'
import { useCreateEstimationScaleMutation } from '@/src/store/features/planning/estimation-scales-api'
import { toFormErrors, toApiError } from '@/src/utils'
import { Form, Modal } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import { useModalForm } from '@/src/hooks'
import EstimationScaleValuesField, {
  estimationScaleValueErrorFieldFor,
  newEstimationScaleValue,
} from './estimation-scale-values-field'

const { Item } = Form

export interface CreateEstimationScaleFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CreateEstimationScaleFormValues {
  name: string
  description?: string
  values: EstimationScaleValueRequest[]
}

const CreateEstimationScaleForm = ({
  onFormComplete,
  onFormCancel,
}: CreateEstimationScaleFormProps) => {
  const messageApi = useMessage()

  const [createEstimationScale] = useCreateEstimationScaleMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateEstimationScaleFormValues>({
      onSubmit: async (values: CreateEstimationScaleFormValues, form) => {
          try {
            const request: CreateEstimationScaleRequest = {
              name: values.name,
              description: values.description,
              values: values.values,
            }
            const response = await createEstimationScale(request)
            if (response.error) {
              throw response.error
            }
            messageApi.success('Estimation scale created successfully.')
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
                  'An error occurred while creating the estimation scale. Please try again.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while creating the estimation scale. Please try again.',
      permission: 'Permissions.EstimationScales.Create',
    })

  return (
    <Modal
      title="Create Estimation Scale"
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
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="create-estimation-scale-form"
        initialValues={{
          values: [newEstimationScaleValue(), newEstimationScaleValue()],
        }}
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
        <Item
          name="description"
          label="Description"
          rules={[{ max: 1024 }]}
        >
          <TextArea
            autoSize={{ minRows: 3, maxRows: 6 }}
            showCount
            maxLength={1024}
          />
        </Item>
        <EstimationScaleValuesField extra="Mark a value as a card when it can be voted but never becomes an estimate, such as ?." />
      </Form>
    </Modal>
  )
}

export default CreateEstimationScaleForm
