'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { CreateStatusWorkflowRequest } from '@/src/services/wayd-api'
import {
  useCreateStatusWorkflowMutation,
  useGetWorkflowOwnerTypesQuery,
} from '@/src/store/features/common/status-workflows-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal, Select } from 'antd'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input

export interface CreateStatusWorkflowFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CreateStatusWorkflowFormValues {
  name: string
  description?: string
  ownerType: string
}

const mapToRequestValues = (
  values: CreateStatusWorkflowFormValues,
): CreateStatusWorkflowRequest => {
  return {
    name: values.name,
    description: values.description,
    ownerType: values.ownerType,
  } as CreateStatusWorkflowRequest
}

const CreateStatusWorkflowForm = ({
  onFormComplete,
  onFormCancel,
}: CreateStatusWorkflowFormProps) => {
  const messageApi = useMessage()

  const { data: ownerTypes, isLoading: ownerTypesLoading } =
    useGetWorkflowOwnerTypesQuery()

  const [createStatusWorkflow] = useCreateStatusWorkflowMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateStatusWorkflowFormValues>({
      onSubmit: async (values: CreateStatusWorkflowFormValues, form) => {
        try {
          const request = mapToRequestValues(values)
          const response = await createStatusWorkflow(request)
          if (response.error) {
            throw response.error
          }
          messageApi.success('Status Workflow created successfully.')
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
                'An error occurred while creating the status workflow. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while creating the status workflow. Please try again.',
      permission: 'Permissions.StatusWorkflows.Create',
    })

  return (
    <Modal
      title="Create Status Workflow"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="create-status-workflow-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 128 }]}
        >
          <Input showCount maxLength={128} />
        </Item>
        <Item
          label="Owner Type"
          name="ownerType"
          tooltip="The kind of record this workflow governs. It is fixed once the workflow is created — a workflow's statuses only mean anything within its owner type."
          rules={[{ required: true, message: 'Owner Type is required' }]}
        >
          <Select
            loading={ownerTypesLoading}
            placeholder="Select an owner type"
            options={(ownerTypes ?? []).map((o) => ({
              value: o.key,
              label: o.displayName,
            }))}
          />
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

export default CreateStatusWorkflowForm
