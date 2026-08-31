'use client'

import { MarkdownEditor } from '@/src/components/common/markdown'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { CreateProductRequest } from '@/src/services/wayd-api'
import {
  useCreateProductMutation,
  useGetProductsQuery,
} from '@/src/store/features/product-management/products-api'
import { useGetProductTypesQuery } from '@/src/store/features/product-management/product-types-api'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Modal, Select } from 'antd'
import TextArea from 'antd/es/input/TextArea'

const { Item } = Form

export interface CreateProductFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
  /** Preselects the parent, when creating from within a product's page. */
  defaultParentId?: string
}

interface CreateProductFormValues {
  name: string
  description?: string
  productTypeId: string
  parentId?: string
}

const mapToRequestValues = (
  values: CreateProductFormValues,
): CreateProductRequest =>
  ({
    name: values.name,
    description: values.description,
    productTypeId: values.productTypeId,
    parentId: values.parentId,
  }) as CreateProductRequest

const CreateProductForm = ({
  onFormComplete,
  onFormCancel,
  defaultParentId,
}: CreateProductFormProps) => {
  const messageApi = useMessage()

  const [createProduct] = useCreateProductMutation()

  // Active types only: an inactive one is retired from new use and the API refuses it.
  const { data: productTypes } = useGetProductTypesQuery(true)
  const { data: products } = useGetProductsQuery(undefined)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateProductFormValues>({
      onSubmit: async (values: CreateProductFormValues, form) => {
        try {
          const request = mapToRequestValues(values)
          const response = await createProduct(request)
          if (response.error) throw response.error

          messageApi.success(
            'Product created successfully. Product key: ' + response.data!.key,
          )
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
                'An error occurred while creating the product. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while creating the product. Please try again.',
      permission: 'Permissions.Products.Create',
    })

  const typeOptions = (productTypes ?? [])
    .map((t) => ({ value: t.id, label: t.name }))
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))

  const parentOptions = (products ?? [])
    .map((p) => ({ value: p.id, label: p.name }))
    .sort((a, b) => caseInsensitiveCompare(a.label, b.label))

  return (
    <Modal
      title="Create Product"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="create-product-form"
        initialValues={defaultParentId ? { parentId: defaultParentId } : undefined}
      >
        <Item name="parentId" label="Parent">
          <Select
            options={parentOptions}
            placeholder="Select a parent"
            allowClear
            showSearch
            optionFilterProp="label"
          />
        </Item>
        <Item
          label="Type"
          name="productTypeId"
          rules={[{ required: true, message: 'Type is required' }]}
          // The type decides whether releases can be cut against the product, which is why it is
          // required up front rather than editable later as a detail.
          extra="Decides whether releases can be cut against this product."
        >
          <Select
            options={typeOptions}
            placeholder="Select a type"
            showSearch
            optionFilterProp="label"
          />
        </Item>
        <Item
          label="Name"
          name="name"
          rules={[{ required: true, message: 'Name is required' }, { max: 128 }]}
        >
          <TextArea
            autoSize={{ minRows: 1, maxRows: 2 }}
            showCount
            maxLength={128}
          />
        </Item>
        <Item name="description" label="Description" rules={[{ max: 1024 }]}>
          <MarkdownEditor maxLength={1024} />
        </Item>
      </Form>
    </Modal>
  )
}

export default CreateProductForm
