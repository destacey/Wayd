'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { UpdateProductTagCategoryRequest } from '@/src/services/wayd-api'
import { useUpdateProductTagCategoryMutation } from '@/src/store/features/product-management/product-tag-categories-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useModalForm } from '@/src/hooks'
import { useEffect } from 'react'
import { ProductTagCategoryActionTarget } from './types'

const { Item } = Form
const { TextArea } = Input

export interface EditProductTagCategoryFormProps {
  /**
   * The axis to edit. Passed whole rather than fetched by id: the list query
   * is the only read of a category, so the caller already holds the record and
   * a lookup here would be a second copy of it.
   */
  category: ProductTagCategoryActionTarget
  onFormComplete: () => void
  onFormCancel: () => void
}

interface UpdateProductTagCategoryFormValues {
  name: string
  description?: string
}

const mapToRequestValues = (
  values: UpdateProductTagCategoryFormValues,
  id: string,
): UpdateProductTagCategoryRequest => ({
  id,
  name: values.name,
  description: values.description,
})

const EditProductTagCategoryForm = ({
  category,
  onFormComplete,
  onFormCancel,
}: EditProductTagCategoryFormProps) => {
  const messageApi = useMessage()

  const [updateProductTagCategory] = useUpdateProductTagCategoryMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<UpdateProductTagCategoryFormValues>({
      onSubmit: async (values, form) => {
        try {
          const response = await updateProductTagCategory(
            mapToRequestValues(values, category.id),
          )
          if (response.error) {
            throw response.error
          }
          messageApi.success('Tag category updated successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
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
      permission: 'Permissions.ProductTagCategories.Update',
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
        name="update-product-tag-category-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 64 }]}
          // Safe on an axis in use: products reference their tags by id, so
          // the new name shows everywhere at once.
          extra="Renaming is safe — products carry the axis by reference."
        >
          <TextArea autoSize={{ minRows: 1, maxRows: 2 }} showCount maxLength={64} />
        </Item>
        <Item
          name="description"
          label="Description"
          rules={[{ max: 512 }]}
          extra="Cleared when left empty."
        >
          <TextArea autoSize={{ minRows: 3, maxRows: 6 }} showCount maxLength={512} />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditProductTagCategoryForm
