'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { IntakeRequestTagListItemDto } from '@/src/services/wayd-api'
import { useUpdateFreeIntakeRequestTagMutation } from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useModalForm } from '@/src/hooks'
import { useEffect } from 'react'

const { Item } = Form
const { TextArea } = Input

export interface EditFreeTagFormProps {
  tag: IntakeRequestTagListItemDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditFreeTagFormValues {
  name: string
  description?: string
}

/** Renames and re-describes a free tag. */
const EditFreeTagForm = ({
  tag,
  onFormComplete,
  onFormCancel,
}: EditFreeTagFormProps) => {
  const messageApi = useMessage()

  const [updateFreeTag] = useUpdateFreeIntakeRequestTagMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditFreeTagFormValues>({
      onSubmit: async (values, form) => {
        try {
          await updateFreeTag({
            id: tag.id,
            name: values.name,
            description: values.description?.trim() || undefined,
          }).unwrap()
          messageApi.success('Tag updated.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while updating the tag. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the tag. Please try again.',
      permission: 'Permissions.IntakeRequests.Administer',
    })

  useEffect(() => {
    form.setFieldsValue({ name: tag.name, description: tag.description })
  }, [tag, form])

  return (
    <Modal
      title="Edit Free Tag"
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
        name="edit-free-intake-request-tag-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 64 }]}
          extra="Must be unique among free tags. To fold this tag into one that already has the name, merge it instead."
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

export default EditFreeTagForm
