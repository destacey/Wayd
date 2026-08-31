'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { ProductDto, ProductTagCategoryDto } from '@/src/services/wayd-api'
import { useGetProductTagCategoriesQuery } from '@/src/store/features/product-management/product-tag-categories-api'
import {
  useTagProductMutation,
  useUntagProductMutation,
} from '@/src/store/features/product-management/products-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Modal, Select, Spin } from 'antd'
import { useEffect } from 'react'

const { Item } = Form

export interface ManageProductTagsFormProps {
  product: ProductDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * One field per category, keyed by category id. A single-value category holds a
 * tag id or undefined; an `allowsMany` one holds an array.
 */
type ManageProductTagsFormValues = Record<string, string | string[] | undefined>

const toTagIds = (value: string | string[] | undefined): string[] => {
  if (Array.isArray(value)) return value
  return value ? [value] : []
}

/**
 * The tags a product already carries, grouped by category id.
 */
const currentTagIdsByCategory = (product: ProductDto) => {
  const grouped = new Map<string, string[]>()
  for (const tag of product.tags) {
    grouped.set(tag.categoryId, [
      ...(grouped.get(tag.categoryId) ?? []),
      tag.tagId,
    ])
  }
  return grouped
}

const initialValues = (
  product: ProductDto,
  categories: ProductTagCategoryDto[],
): ManageProductTagsFormValues => {
  const current = currentTagIdsByCategory(product)
  const values: ManageProductTagsFormValues = {}
  for (const category of categories) {
    const tagIds = current.get(category.id) ?? []
    values[category.id] = category.allowsMany ? tagIds : tagIds[0]
  }
  return values
}

/**
 * Tag options offered for a category: the active ones, plus any inactive tag the
 * product already carries. Dropping the latter would make it invisible here while
 * still being carried, and reopening the form would then untag it unasked.
 */
const optionsFor = (category: ProductTagCategoryDto, carried: string[]) =>
  category.tags
    .filter((tag) => tag.isActive || carried.includes(tag.id))
    .map((tag) => ({
      value: tag.id,
      label: tag.isActive ? tag.name : `${tag.name} (inactive)`,
    }))

/**
 * Applies a product's tags across every active category at once.
 *
 * The API tags and untags one tag at a time, so a submit fans out into a call per
 * change. Only differences are sent — an unchanged category costs nothing.
 */
const ManageProductTagsForm = ({
  product,
  onFormComplete,
  onFormCancel,
}: ManageProductTagsFormProps) => {
  const messageApi = useMessage()

  const [tagProduct] = useTagProductMutation()
  const [untagProduct] = useUntagProductMutation()

  const { data: categories, isLoading } = useGetProductTagCategoriesQuery(true)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<ManageProductTagsFormValues>({
      onSubmit: async (values: ManageProductTagsFormValues, form) => {
        try {
          const current = currentTagIdsByCategory(product)

          const added: string[] = []
          const removed: string[] = []
          for (const category of categories ?? []) {
            const before = current.get(category.id) ?? []
            const after = toTagIds(values[category.id])
            added.push(...after.filter((id) => !before.includes(id)))
            removed.push(...before.filter((id) => !after.includes(id)))
          }

          for (const tagId of removed) {
            const response = await untagProduct({ id: product.id, tagId })
            if (response.error) throw response.error
          }
          for (const tagId of added) {
            const response = await tagProduct({ id: product.id, tagId })
            if (response.error) throw response.error
          }

          messageApi.success('Product tags updated successfully.')
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
                'An error occurred while updating the product tags. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the product tags. Please try again.',
      permission: 'Permissions.Products.Update',
    })

  useEffect(() => {
    if (!categories) return
    form.setFieldsValue(initialValues(product, categories))
  }, [product, categories, form])

  const current = currentTagIdsByCategory(product)

  return (
    <Modal
      title="Manage Tags"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Spin spinning={isLoading}>
        <Form
          form={form}
          size="small"
          layout="vertical"
          name="manage-product-tags-form"
        >
          {(categories ?? []).map((category) => (
            <Item
              key={category.id}
              name={category.id}
              label={category.name}
              extra={category.description}
            >
              <Select
                mode={category.allowsMany ? 'multiple' : undefined}
                allowClear
                options={optionsFor(category, current.get(category.id) ?? [])}
                placeholder={`Select ${category.name}`}
                optionFilterProp="label"
              />
            </Item>
          ))}
        </Form>
      </Spin>
    </Modal>
  )
}

export default ManageProductTagsForm
