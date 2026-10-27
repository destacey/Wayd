'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useUpdateIntakeRequestTagCategoryMutation } from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useModalForm } from '@/src/hooks'
import { useEffect } from 'react'
import { TagAxisActionTarget } from './types'

const { Item } = Form
const { TextArea } = Input

export interface EditTagAxisFormProps {
  /**
   * The axis to edit. Passed whole rather than fetched by id: the list query
   * is the only read of an axis, so the caller already holds the record.
   */
  axis: TagAxisActionTarget
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditTagAxisFormValues {
  name: string
  description?: string
}

/** Renames and re-describes a tag axis the organization added. */
const EditTagAxisForm = ({
  axis,
  onFormComplete,
  onFormCancel,
}: EditTagAxisFormProps) => {
  const messageApi = useMessage()

  const [updateTagAxis] = useUpdateIntakeRequestTagCategoryMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditTagAxisFormValues>({
      onSubmit: async (values, form) => {
        try {
          await updateTagAxis({
            id: axis.id,
            name: values.name,
            description: values.description?.trim() || undefined,
          }).unwrap()
          messageApi.success('Tag axis updated.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while updating the tag axis. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the tag axis. Please try again.',
      permission: 'Permissions.IntakeRequests.Administer',
    })

  useEffect(() => {
    form.setFieldsValue({ name: axis.name, description: axis.description })
  }, [axis, form])

  return (
    <Modal
      title="Edit Tag Axis"
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
        name="edit-intake-request-tag-axis-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 64 }]}
          extra="Renaming is safe — requests carry the axis by reference."
        >
          <Input showCount maxLength={64} />
        </Item>
        <Item
          name="description"
          label="Description"
          rules={[{ max: 512 }]}
          extra="Cleared when left empty."
        >
          <TextArea autoSize={{ minRows: 3, maxRows: 6 }} maxLength={512} />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditTagAxisForm
