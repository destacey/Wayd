'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { ProductTagListItemDto } from '@/src/services/wayd-api'
import { useUpdateFreeProductTagMutation } from '@/src/store/features/product-management/product-tags-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useModalForm } from '@/src/hooks'
import { useEffect } from 'react'

const { Item } = Form
const { TextArea } = Input

export interface EditFreeProductTagFormProps {
  tag: ProductTagListItemDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditFreeProductTagFormValues {
  name: string
  description?: string
}

/** Renames a free tag. Products carry it by id, so every one shows the new name. */
const EditFreeProductTagForm = ({
  tag,
  onFormComplete,
  onFormCancel,
}: EditFreeProductTagFormProps) => {
  const messageApi = useMessage()

  const [updateFreeProductTag] = useUpdateFreeProductTagMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditFreeProductTagFormValues>({
      onSubmit: async (values, form) => {
        try {
          const response = await updateFreeProductTag({
            id: tag.id,
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
        name="edit-free-product-tag-form"
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

export default EditFreeProductTagForm
