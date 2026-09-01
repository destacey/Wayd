'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useAddProductTagMutation } from '@/src/store/features/product-management/product-tag-categories-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input

export interface AddProductTagFormProps {
  categoryId: string
  /** Named in the title, so it is clear which axis the tag lands on. */
  categoryName: string
  onFormComplete: () => void
  onFormCancel: () => void
}

interface AddProductTagFormValues {
  name: string
  description?: string
}

const AddProductTagForm = ({
  categoryId,
  categoryName,
  onFormComplete,
  onFormCancel,
}: AddProductTagFormProps) => {
  const messageApi = useMessage()

  const [addProductTag] = useAddProductTagMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<AddProductTagFormValues>({
      onSubmit: async (values, form) => {
        try {
          const response = await addProductTag({
            categoryId,
            name: values.name,
            description: values.description,
          })
          if (response.error) {
            throw response.error
          }
          messageApi.success('Tag added successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            // A duplicate name on the axis comes back here rather than as a
            // field error — the rule belongs to the category, which only the
            // server can see.
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
      permission: 'Permissions.ProductTagCategories.Update',
    })

  return (
    <Modal
      title={`Add Tag to ${categoryName}`}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Add"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form form={form} size="small" layout="vertical" name="add-product-tag-form">
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 64 }]}
          extra="The label itself — ios, android, pci-scope. Must be unique on this axis."
        >
          <TextArea autoSize={{ minRows: 1, maxRows: 2 }} showCount maxLength={64} />
        </Item>
        <Item
          name="description"
          label="Description"
          rules={[{ max: 512 }]}
          extra="What the label means, where it is not obvious."
        >
          <TextArea autoSize={{ minRows: 3, maxRows: 6 }} showCount maxLength={512} />
        </Item>
      </Form>
    </Modal>
  )
}

export default AddProductTagForm
