'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  ScoringModelOutputDto,
  ScoringModelOutputRequest,
} from '@/src/services/wayd-api'
import { useUpdateScoringModelOutputMutation } from '@/src/store/features/scoring/scoring-models-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Checkbox, Form, Input, Modal, Typography } from 'antd'
import TextArea from 'antd/es/input/TextArea'
import { useEffect } from 'react'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { Text } = Typography

export interface EditScoringModelOutputFormProps {
  scoringModelId: string
  output: ScoringModelOutputDto
  availableTokens: string[]
  onFormComplete: () => void
  onFormCancel: () => void
}

interface UpdateScoringModelOutputFormValues {
  name: string
  token: string
  formula: string
  isPrimary: boolean
}

const mapToRequestValues = (
  values: UpdateScoringModelOutputFormValues,
): ScoringModelOutputRequest => {
  return {
    name: values.name,
    token: values.token,
    formula: values.formula,
    isPrimary: values.isPrimary ?? false,
  } as ScoringModelOutputRequest
}

const EditScoringModelOutputForm = ({
  scoringModelId,
  output,
  availableTokens,
  onFormComplete,
  onFormCancel,
}: EditScoringModelOutputFormProps) => {
  const messageApi = useMessage()

  const [updateScoringModelOutput] = useUpdateScoringModelOutputMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<UpdateScoringModelOutputFormValues>({
      onSubmit: async (values: UpdateScoringModelOutputFormValues, form) => {
        try {
          const request = mapToRequestValues(values)
          const response = await updateScoringModelOutput({
            scoringModelId,
            outputId: output.id,
            ...request,
          })
          if (response.error) {
            throw response.error
          }
          messageApi.success('Output updated successfully.')
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
                'An error occurred while updating the output. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the output. Please try again.',
      permission: 'Permissions.ScoringModels.Update',
    })

  useEffect(() => {
    if (!output) return
    form.setFieldsValue({
      name: output.name,
      token: output.token,
      formula: output.formula,
      isPrimary: output.isPrimary,
    })
  }, [output, form])

  return (
    <Modal
      title="Edit Output"
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
        name="update-scoring-model-output-form"
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

export default EditScoringModelOutputForm
