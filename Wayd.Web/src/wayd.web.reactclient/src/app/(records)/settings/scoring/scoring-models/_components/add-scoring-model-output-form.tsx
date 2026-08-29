'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { ScoringModelOutputRequest } from '@/src/services/wayd-api'
import { useAddScoringModelOutputMutation } from '@/src/store/features/scoring/scoring-models-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Checkbox, Form, Input, Modal, Typography } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { Text } = Typography

export interface AddScoringModelOutputFormProps {
  scoringModelId: string
  availableTokens: string[]
  initialValues?: Partial<AddScoringModelOutputFormValues>
  onFormComplete: () => void
  onFormCancel: () => void
}

interface AddScoringModelOutputFormValues {
  name: string
  token: string
  formula: string
  isPrimary: boolean
}

const mapToRequestValues = (
  values: AddScoringModelOutputFormValues,
): ScoringModelOutputRequest => {
  return {
    name: values.name,
    token: values.token,
    formula: values.formula,
    isPrimary: values.isPrimary ?? false,
  } as ScoringModelOutputRequest
}

const AddScoringModelOutputForm = ({
  scoringModelId,
  availableTokens,
  initialValues,
  onFormComplete,
  onFormCancel,
}: AddScoringModelOutputFormProps) => {
  const messageApi = useMessage()

  const [addScoringModelOutput] = useAddScoringModelOutputMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<AddScoringModelOutputFormValues>({
      onSubmit: async (values: AddScoringModelOutputFormValues, form) => {
        try {
          const request = mapToRequestValues(values)
          const response = await addScoringModelOutput({
            scoringModelId,
            ...request,
          })
          if (response.error) {
            throw response.error
          }
          messageApi.success('Output added successfully.')
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
                'An error occurred while adding the output. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while adding the output. Please try again.',
      permission: 'Permissions.ScoringModels.Update',
    })

  return (
    <Modal
      title="Add Output"
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
        name="add-scoring-model-output-form"
        initialValues={{ isPrimary: false, ...initialValues }}
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
          tooltip="The short identifier later output formulas can reference (e.g., CoD)."
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
          label="Formula"
          name="formula"
          tooltip="An arithmetic expression over criterion tokens and earlier output tokens (e.g., (BV + TC + RR) / JS)."
          rules={[
            { required: true, message: 'Formula is required' },
            { max: 1000 },
          ]}
          extra={
            availableTokens.length > 0 ? (
              <Text type="secondary">
                Available tokens: {availableTokens.join(', ')}
              </Text>
            ) : undefined
          }
        >
          <TextArea autoSize={{ minRows: 2, maxRows: 4 }} maxLength={1000} />
        </Item>
        <Item name="isPrimary" valuePropName="checked">
          <Checkbox>Primary score (the model&apos;s ranking value)</Checkbox>
        </Item>
      </Form>
    </Modal>
  )
}

export default AddScoringModelOutputForm
