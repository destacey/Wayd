'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  StatusCategory,
  WorkflowAliasDto,
  WorkflowStatusDto,
} from '@/src/services/wayd-api'
import {
  useReclassifyWorkflowStatusMutation,
  useRenameWorkflowStatusMutation,
} from '@/src/store/features/common/status-workflows-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal, Select } from 'antd'
import { useEffect } from 'react'
import { useModalForm } from '@/src/hooks'
import { buildAliasOptions, NO_ALIAS } from './workflow-status-alias-options'

const { Item } = Form
const { TextArea } = Input

const categoryOptions = Object.values(StatusCategory).map((c) => ({
  value: c,
  label: c,
}))

export interface EditWorkflowStatusFormProps {
  workflowId: string
  status: WorkflowStatusDto
  statuses: WorkflowStatusDto[]
  aliases: WorkflowAliasDto[]
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditWorkflowStatusFormValues {
  name: string
  description?: string
  category: StatusCategory
  alias: number
}

/**
 * One dialog over two endpoints: renaming and reclassifying are separate on the
 * API, so only the halves that changed are sent.
 */
const EditWorkflowStatusForm = ({
  workflowId,
  status,
  statuses,
  aliases,
  onFormComplete,
  onFormCancel,
}: EditWorkflowStatusFormProps) => {
  const messageApi = useMessage()

  const [renameWorkflowStatus] = useRenameWorkflowStatusMutation()
  const [reclassifyWorkflowStatus] = useReclassifyWorkflowStatusMutation()

  const aliasOptions = buildAliasOptions(aliases, statuses, status.id)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditWorkflowStatusFormValues>({
      onSubmit: async (values: EditWorkflowStatusFormValues, form) => {
        try {
          const alias = values.alias ?? NO_ALIAS

          const renamed =
            values.name !== status.name ||
            (values.description ?? '') !== (status.description ?? '')
          const reclassified =
            values.category !== status.category?.name || alias !== status.alias

          if (renamed) {
            const response = await renameWorkflowStatus({
              workflowId,
              statusId: status.id,
              request: {
                name: values.name,
                description: values.description,
              },
            })
            if (response.error) {
              throw response.error
            }
          }

          if (reclassified) {
            const response = await reclassifyWorkflowStatus({
              workflowId,
              statusId: status.id,
              request: { category: values.category, alias },
            })
            if (response.error) {
              throw response.error
            }
          }

          messageApi.success('Status updated successfully.')
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
                'An error occurred while updating the status. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the status. Please try again.',
      permission: 'Permissions.StatusWorkflows.Update',
    })

  useEffect(() => {
    form.setFieldsValue({
      name: status.name,
      description: status.description,
      category: status.category?.name as StatusCategory,
      alias: status.alias ?? NO_ALIAS,
    })
  }, [status, form])

  return (
    <Modal
      title="Edit Status"
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
        name="edit-workflow-status-form"
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

export default EditWorkflowStatusForm
