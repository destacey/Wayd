'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  CloneStatusWorkflowRequest,
  StatusWorkflowDetailsDto,
} from '@/src/services/wayd-api'
import { useCloneStatusWorkflowMutation } from '@/src/store/features/common/status-workflows-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useEffect } from 'react'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input

export interface CloneStatusWorkflowFormProps {
  statusWorkflow: StatusWorkflowDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CloneStatusWorkflowFormValues {
  name: string
  description?: string
}

const mapToRequestValues = (
  values: CloneStatusWorkflowFormValues,
): CloneStatusWorkflowRequest => {
  return {
    name: values.name,
    description: values.description,
  } as CloneStatusWorkflowRequest
}

/**
 * Cloning is how a published workflow gets changed: it is frozen in place, so
 * the copy inherits its owner type and statuses and only the name differs.
 */
const CloneStatusWorkflowForm = ({
  statusWorkflow,
  onFormComplete,
  onFormCancel,
}: CloneStatusWorkflowFormProps) => {
  const messageApi = useMessage()

  const [cloneStatusWorkflow] = useCloneStatusWorkflowMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CloneStatusWorkflowFormValues>({
      onSubmit: async (values: CloneStatusWorkflowFormValues, form) => {
        try {
          const request = mapToRequestValues(values)
          const response = await cloneStatusWorkflow({
            id: statusWorkflow.id,
            request,
          })
          if (response.error) {
            throw response.error
          }
          messageApi.success('Status Workflow cloned successfully.')
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
                'An error occurred while cloning the status workflow. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while cloning the status workflow. Please try again.',
      permission: 'Permissions.StatusWorkflows.Create',
    })

  useEffect(() => {
    form.setFieldsValue({
      name: `${statusWorkflow.name} (Copy)`,
      description: statusWorkflow.description,
    })
  }, [statusWorkflow, form])

  return (
    <Modal
      title="Clone Status Workflow"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Clone"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="clone-status-workflow-form"
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

export default CloneStatusWorkflowForm
