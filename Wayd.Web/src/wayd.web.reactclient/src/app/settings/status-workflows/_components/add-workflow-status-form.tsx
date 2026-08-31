'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  AddWorkflowStatusRequest,
  StatusCategory,
  WorkflowAliasDto,
  WorkflowStatusDto,
} from '@/src/services/wayd-api'
import { useAddWorkflowStatusMutation } from '@/src/store/features/common/status-workflows-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal, Select } from 'antd'
import { useModalForm } from '@/src/hooks'
import { buildAliasOptions, NO_ALIAS } from './workflow-status-alias-options'

const { Item } = Form
const { TextArea } = Input

const categoryOptions = Object.values(StatusCategory).map((c) => ({
  value: c,
  label: c,
}))

export interface AddWorkflowStatusFormProps {
  workflowId: string
  statuses: WorkflowStatusDto[]
  aliases: WorkflowAliasDto[]
  onFormComplete: () => void
  onFormCancel: () => void
}

interface AddWorkflowStatusFormValues {
  name: string
  description?: string
  category: StatusCategory
  alias: number
}

const mapToRequestValues = (
  values: AddWorkflowStatusFormValues,
): AddWorkflowStatusRequest => {
  return {
    name: values.name,
    description: values.description,
    category: values.category,
    alias: values.alias ?? NO_ALIAS,
  } as AddWorkflowStatusRequest
}

const AddWorkflowStatusForm = ({
  workflowId,
  statuses,
  aliases,
  onFormComplete,
  onFormCancel,
}: AddWorkflowStatusFormProps) => {
  const messageApi = useMessage()

  const [addWorkflowStatus] = useAddWorkflowStatusMutation()

  const aliasOptions = buildAliasOptions(aliases, statuses)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<AddWorkflowStatusFormValues>({
      onSubmit: async (values: AddWorkflowStatusFormValues, form) => {
        try {
          const request = mapToRequestValues(values)
          const response = await addWorkflowStatus({ workflowId, request })
          if (response.error) {
            throw response.error
          }
          messageApi.success('Status added successfully.')
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
                'An error occurred while adding the status. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while adding the status. Please try again.',
      permission: 'Permissions.StatusWorkflows.Update',
    })

  return (
    <Modal
      title="Add Status"
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
        name="add-workflow-status-form"
        initialValues={{ alias: NO_ALIAS }}
      >
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 128 }]}
        >
          <Input showCount maxLength={128} />
        </Item>
        <Item
          label="Category"
          name="category"
          tooltip="The high-level bucket the status belongs to. Reports and metrics read the category rather than the status name."
          rules={[{ required: true, message: 'Category is required' }]}
        >
          <Select placeholder="Select a category" options={categoryOptions} />
        </Item>
        <Item
          label="Meaning"
          name="alias"
          tooltip="The well-known meaning this status carries for its owner type. Only one status can hold each meaning, so meanings already taken are not listed."
        >
          <Select options={aliasOptions} />
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

export default AddWorkflowStatusForm
