'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  StatusWorkflowDetailsDto,
  UpdateStatusWorkflowRequest,
} from '@/src/services/wayd-api'
import { useUpdateStatusWorkflowMutation } from '@/src/store/features/common/status-workflows-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useEffect } from 'react'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input

export interface EditStatusWorkflowFormProps {
  statusWorkflow: StatusWorkflowDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface UpdateStatusWorkflowFormValues {
  name: string
  description?: string
}

const mapToRequestValues = (
  values: UpdateStatusWorkflowFormValues,
): UpdateStatusWorkflowRequest => {
  return {
    name: values.name,
    description: values.description,
  } as UpdateStatusWorkflowRequest
}

const EditStatusWorkflowForm = ({
  statusWorkflow,
  onFormComplete,
  onFormCancel,
}: EditStatusWorkflowFormProps) => {
  const messageApi = useMessage()

  const [updateStatusWorkflow] = useUpdateStatusWorkflowMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<UpdateStatusWorkflowFormValues>({
      onSubmit: async (values: UpdateStatusWorkflowFormValues, form) => {
        try {
          const request = mapToRequestValues(values)
          const response = await updateStatusWorkflow({
            id: statusWorkflow.id,
            request,
          })
          if (response.error) {
            throw response.error
          }
          messageApi.success('Status Workflow updated successfully.')
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
                'An error occurred while updating the status workflow. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the status workflow. Please try again.',
      permission: 'Permissions.StatusWorkflows.Update',
    })

  useEffect(() => {
    form.setFieldsValue({
      name: statusWorkflow.name,
      description: statusWorkflow.description,
    })
  }, [statusWorkflow, form])

  return (
    <Modal
      title="Edit Status Workflow"
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
        name="update-status-workflow-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 128 }]}
        >
          <Input showCount maxLength={128} />
        </Item>
        <Item name="description" label="Description" rules={[{ max: 1024 }]}>
          <TextArea
            autoSize={{ minRows: 4, maxRows: 8 }}
            showCount
            maxLength={1024}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditStatusWorkflowForm
