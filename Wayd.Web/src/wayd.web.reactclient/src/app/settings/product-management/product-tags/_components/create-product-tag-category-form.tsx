'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { CreateProductTagCategoryRequest } from '@/src/services/wayd-api'
import { useCreateProductTagCategoryMutation } from '@/src/store/features/product-management/product-tag-categories-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal, Switch } from 'antd'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input

export interface CreateProductTagCategoryFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

interface CreateProductTagCategoryFormValues {
  name: string
  description?: string
  allowsMany: boolean
}

const mapToRequestValues = (
  values: CreateProductTagCategoryFormValues,
): CreateProductTagCategoryRequest => ({
  name: values.name,
  description: values.description,
  allowsMany: values.allowsMany,
})

const CreateProductTagCategoryForm = ({
  onFormComplete,
  onFormCancel,
}: CreateProductTagCategoryFormProps) => {
  const messageApi = useMessage()

  const [createProductTagCategory] = useCreateProductTagCategoryMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateProductTagCategoryFormValues>({
      onSubmit: async (values, form) => {
        try {
          const response = await createProductTagCategory(
            mapToRequestValues(values),
          )
          if (response.error) {
            throw response.error
          }
          messageApi.success('Tag category created successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while creating the tag category. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while creating the tag category. Please try again.',
      permission: 'Permissions.ProductTagCategories.Create',
    })


  return (
    <Modal
      title="Create Tag Category"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="create-product-tag-category-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 64 }]}
          extra="The axis products are labelled along — Platform, Tech Stack, Compliance."
        >
          <TextArea autoSize={{ minRows: 1, maxRows: 2 }} showCount maxLength={64} />
        </Item>
        <Item name="description" label="Description" rules={[{ max: 512 }]}>
          <TextArea autoSize={{ minRows: 3, maxRows: 6 }} showCount maxLength={512} />
        </Item>
        <Item
          name="allowsMany"
          label="Allow multiple tags?"
          valuePropName="checked"
          initialValue={false}
          // Fixed once set: narrowing it later would leave products holding
          // more tags than the axis permits, so the edit form does not offer
          // it and this is the only chance to choose.
          extra="Whether a product can carry several tags from this axis. Cannot be changed later."
        >
          <Switch checkedChildren="Yes" unCheckedChildren="No" />
        </Item>
      </Form>
    </Modal>
  )
}

export default CreateProductTagCategoryForm
