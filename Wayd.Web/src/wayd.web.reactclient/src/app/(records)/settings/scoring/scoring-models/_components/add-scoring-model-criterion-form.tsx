'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  ScoringModelCriterionRequest,
  ScoringScaleDto,
} from '@/src/services/wayd-api'
import { useAddScoringModelCriterionMutation } from '@/src/store/features/scoring/scoring-models-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, InputNumber, Modal, Select } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface AddScoringModelCriterionFormProps {
  scoringModelId: string
  scales: ScoringScaleDto[]
  onFormComplete: () => void
  onFormCancel: () => void
}

interface AddScoringModelCriterionFormValues {
  name: string
  token: string
  description?: string
  weight?: number
  scaleId?: string
}

const mapToRequestValues = (
  values: AddScoringModelCriterionFormValues,
): ScoringModelCriterionRequest => {
  return {
    name: values.name,
    token: values.token,
    description: values.description,
    weight: values.weight,
    scaleId: values.scaleId,
  } as ScoringModelCriterionRequest
}

const AddScoringModelCriterionForm = ({
  scoringModelId,
  scales,
  onFormComplete,
  onFormCancel,
}: AddScoringModelCriterionFormProps) => {
  const messageApi = useMessage()

  const [addScoringModelCriterion] = useAddScoringModelCriterionMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<AddScoringModelCriterionFormValues>({
      onSubmit: async (values: AddScoringModelCriterionFormValues, form) => {
        try {
          const request = mapToRequestValues(values)
          const response = await addScoringModelCriterion({
            scoringModelId,
            ...request,
          })
          if (response.error) {
            throw response.error
          }
          messageApi.success('Criterion added successfully.')
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
                'An error occurred while adding the criterion. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while adding the criterion. Please try again.',
      permission: 'Permissions.ScoringModels.Update',
    })

  return (
    <Modal
      title="Add Criterion"
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
        name="add-scoring-model-criterion-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 128 }]}
        >
          <Input showCount maxLength={128} />
        </Item>
        <Item
          label="Token"
          name="token"
          tooltip="The short identifier used to reference this criterion in output formulas (e.g., BV)."
          rules={[
            { required: true, message: 'Token is required' },
            { max: 32 },
            {
              pattern: /^[A-Za-z_][A-Za-z0-9_]*$/,
              message:
                'Tokens must start with a letter or underscore and contain only letters, digits, or underscores.',
            },
          ]}
        >
          <Input maxLength={32} />
        </Item>
        <Item
          label="Rating Scale"
          name="scaleId"
          tooltip="The scale this criterion is rated against. Leave as free numeric entry to type a number when scoring."
        >
          <Select
            allowClear
            placeholder="Free numeric entry"
            options={scales.map((s) => ({ value: s.id, label: s.name }))}
          />
        </Item>
        <Item
          label="Weight (%)"
          name="weight"
          tooltip="Optional. Only used by the weighted-formula scaffolder; the output formulas determine the score."
        >
          <InputNumber min={0} max={100} style={{ width: '100%' }} />
        </Item>
        <Item name="description" label="Description" rules={[{ max: 1024 }]}>
          <TextArea
            autoSize={{ minRows: 3, maxRows: 6 }}
            showCount
            maxLength={1024}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default AddScoringModelCriterionForm
