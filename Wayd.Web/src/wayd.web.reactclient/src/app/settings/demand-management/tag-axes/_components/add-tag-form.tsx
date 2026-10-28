'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useAddIntakeRequestTagMutation } from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input

export interface AddTagFormProps {
  axisId: string
  /** Named in the title, so it is clear which axis the tag lands on. */
  axisName: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface AddTagFormValues {
  name: string
  description?: string
}

/** Adds a tag to a tag axis. */
const AddTagForm = ({
  axisId,
  axisName,
  onFormComplete,
  onFormCancel,
}: AddTagFormProps) => {
  const messageApi = useMessage()

  const [addTag] = useAddIntakeRequestTagMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<AddTagFormValues>({
      onSubmit: async (values, form) => {
        try {
          await addTag({
            categoryId: axisId,
            name: values.name,
            description: values.description?.trim() || undefined,
          }).unwrap()
          messageApi.success('Tag added.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            // A duplicate name on the axis arrives here rather than as a field
            // error: only the server sees the axis's other tags.
            messageApi.error(
              apiError.detail ??
                'An error occurred while adding the tag. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'An error occurred while adding the tag. Please try again.',
      permission: 'Permissions.IntakeRequests.Administer',
    })

  return (
    <Modal
      title={`Add Tag to ${axisName}`}
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
        name="add-intake-request-tag-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 64 }]}
          extra="The label itself — Payments, Enterprise. Must be unique on this axis."
        >
          <Input showCount maxLength={64} />
        </Item>
        <Item
          name="description"
          label="Description"
          rules={[{ max: 512 }]}
          extra="What the label means, where it is not obvious."
        >
          <TextArea autoSize={{ minRows: 3, maxRows: 6 }} maxLength={512} />
        </Item>
      </Form>
    </Modal>
  )
}

export default AddTagForm
