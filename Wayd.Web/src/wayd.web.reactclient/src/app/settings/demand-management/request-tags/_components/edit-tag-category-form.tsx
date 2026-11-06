'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useUpdateIntakeRequestTagCategoryMutation } from '@/src/store/features/demand-management/demand-management-api'
import { toApiError, toFormErrors } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useModalForm } from '@/src/hooks'
import { useEffect } from 'react'
import { TagCategoryActionTarget } from './types'

const { Item } = Form
const { TextArea } = Input

export interface EditTagCategoryFormProps {
  /**
   * The category to edit. Passed whole rather than fetched by id: the list query
   * is the only read of a category, so the caller already holds the record.
   */
  category: TagCategoryActionTarget
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditTagCategoryFormValues {
  name: string
  description?: string
}

/** Renames and re-describes a tag category the organization added. */
const EditTagCategoryForm = ({
  category,
  onFormComplete,
  onFormCancel,
}: EditTagCategoryFormProps) => {
  const messageApi = useMessage()

  const [updateTagCategory] = useUpdateIntakeRequestTagCategoryMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditTagCategoryFormValues>({
      onSubmit: async (values, form) => {
        try {
          await updateTagCategory({
            id: category.id,
            name: values.name,
            description: values.description?.trim() || undefined,
          }).unwrap()
          messageApi.success('Tag category updated.')
          return true
        } catch (error) {
          const apiError = toApiError(error)
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while updating the tag category. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the tag category. Please try again.',
      permission: 'Permissions.IntakeRequests.Administer',
    })

  useEffect(() => {
    form.setFieldsValue({
      name: category.name,
      description: category.description,
    })
  }, [category, form])

  return (
    <Modal
      title="Edit Tag Category"
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
        name="edit-intake-request-tag-category-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 64 }]}
          extra="Renaming is safe — requests carry the category by reference."
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

export default EditTagCategoryForm
