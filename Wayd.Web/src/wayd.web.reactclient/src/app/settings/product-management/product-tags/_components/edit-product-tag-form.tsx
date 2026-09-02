'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { ProductTagOptionDto } from '@/src/services/wayd-api'
import { useRenameProductTagMutation } from '@/src/store/features/product-management/product-tag-categories-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useModalForm } from '@/src/hooks'
import { useEffect } from 'react'

const { Item } = Form
const { TextArea } = Input

export interface EditProductTagFormProps {
  categoryId: string
  tag: ProductTagOptionDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditProductTagFormValues {
  name: string
  description?: string
}

const EditProductTagForm = ({
  categoryId,
  tag,
  onFormComplete,
  onFormCancel,
}: EditProductTagFormProps) => {
  const messageApi = useMessage()

  const [renameProductTag] = useRenameProductTagMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditProductTagFormValues>({
      onSubmit: async (values, form) => {
        try {
          const response = await renameProductTag({
            categoryId,
            tagId: tag.id,
            name: values.name,
            description: values.description,
          })
          if (response.error) {
            throw response.error
          }
          messageApi.success('Tag updated successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
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
      permission: 'Permissions.ProductTagCategories.Update',
    })

  useEffect(() => {
    form.setFieldsValue({ name: tag.name, description: tag.description })
  }, [tag, form])

  return (
    <Modal
      title="Edit Tag"
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
        name="edit-product-tag-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 64 }]}
          // Products reference the tag by id, so a rename reaches every one of
          // them at once — which is the point of a curated list over free text.
          extra={
            tag.productCount > 0
              ? `Renaming is safe — the new name shows on all ${tag.productCount} tagged product(s) at once.`
              : 'Must be unique on this axis.'
          }
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

export default EditProductTagForm
