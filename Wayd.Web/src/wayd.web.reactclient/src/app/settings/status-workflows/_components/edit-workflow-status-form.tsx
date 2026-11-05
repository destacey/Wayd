'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  StatusCategory,
  StatusCategoryLabelDto,
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
import {
  statusCategoryFromId,
  statusCategoryOptions,
} from '@/src/components/common/status-workflows'
import {
  aliasFitsCategory,
  buildAliasOptions,
  NO_ALIAS,
} from './workflow-status-alias-options'

const { Item } = Form
const { TextArea } = Input

export interface EditWorkflowStatusFormProps {
  workflowId: string
  status: WorkflowStatusDto
  statuses: WorkflowStatusDto[]
  aliases: WorkflowAliasDto[]
  /** What the workflow's owner type calls each category. */
  categoryLabels?: StatusCategoryLabelDto[]
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
  categoryLabels,
  onFormComplete,
  onFormCancel,
}: EditWorkflowStatusFormProps) => {
  const messageApi = useMessage()

  const [renameWorkflowStatus] = useRenameWorkflowStatusMutation()
  const [reclassifyWorkflowStatus] = useReclassifyWorkflowStatusMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditWorkflowStatusFormValues>({
      onSubmit: async (values: EditWorkflowStatusFormValues, form) => {
        try {
          const alias = values.alias ?? NO_ALIAS

          const renamed =
            values.name !== status.name ||
            (values.description ?? '') !== (status.description ?? '')
          const reclassified =
            values.category !== statusCategoryFromId(status.category?.id) ||
            alias !== status.alias

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

  // The watch reports nothing until the form inside the modal has mounted and
  // been filled, so the saved values stand in until then.
  const category =
    (Form.useWatch('category', form) as StatusCategory | undefined) ??
    statusCategoryFromId(status.category?.id)
  const alias =
    (Form.useWatch('alias', form) as number | undefined) ?? status.alias
  const aliasOptions = buildAliasOptions(
    aliases,
    statuses,
    category,
    status.id,
    categoryLabels,
  )
  const isMisfiled = !aliasFitsCategory(aliases, alias, category)
  const isUnknown =
    isMisfiled && !(aliases ?? []).some((a) => a.value === alias)

  // A meaning belongs to one category, so changing the category drops a
  // meaning that does not belong to the new one, and moving to a category the
  // status's saved meaning belongs to brings that meaning back.
  const handleValuesChange = (
    changed: Partial<EditWorkflowStatusFormValues>,
  ) => {
    if (!changed.category) return

    const current = form.getFieldValue('alias') as number | undefined
    if (!aliasFitsCategory(aliases, current, changed.category)) {
      form.setFieldValue('alias', NO_ALIAS)
    }

    const saved = status.alias ?? NO_ALIAS
    if (
      form.getFieldValue('alias') === NO_ALIAS &&
      saved !== NO_ALIAS &&
      aliasFitsCategory(aliases, saved, changed.category)
    ) {
      form.setFieldValue('alias', saved)
    }
  }

  useEffect(() => {
    form.setFieldsValue({
      name: status.name,
      description: status.description,
      category: statusCategoryFromId(status.category?.id),
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
        onValuesChange={handleValuesChange}
        name="edit-workflow-status-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[
            { required: true, message: 'Name is required' },
            { max: 128 },
          ]}
        >
          <Input showCount maxLength={128} />
        </Item>
        <Item
          label="Category"
          name="category"
          tooltip="The high-level bucket the status belongs to. Reports and metrics read the category rather than the status name."
          rules={[{ required: true, message: 'Category is required' }]}
        >
          <Select
            placeholder="Select a category"
            options={statusCategoryOptions(categoryLabels)}
          />
        </Item>
        <Item
          label="Meaning"
          name="alias"
          tooltip="The well-known meaning this status carries for its owner type. Each meaning belongs to one category, and only one status can hold it, so only the chosen category's untaken meanings are listed."
          validateStatus={isMisfiled ? 'warning' : undefined}
          help={
            isMisfiled
              ? isUnknown
                ? 'This meaning no longer exists. Choose None or another meaning.'
                : 'This meaning belongs under another category. Move the status there, or choose another meaning.'
              : undefined
          }
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
