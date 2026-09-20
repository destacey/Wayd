'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  AddProductDependencyRequest,
  DependencyStrength,
  InteractionStyle,
  ProductDto,
} from '@/src/services/wayd-api'
import {
  useAddProductDependencyMutation,
  useGetProductsQuery,
} from '@/src/store/features/product-management/products-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { DatePicker, Form, Input, Modal } from 'antd'
import dayjs, { Dayjs } from 'dayjs'
import { ancestorIdsOf, ProductTreeSelect } from '../../_components'
import { DependencyStrengthRadio } from './dependency-strength'
import { InteractionStyleCheckboxes } from './interaction-style'

const { Item } = Form
const { TextArea } = Input

export interface AddProductDependencyFormProps {
  product: ProductDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface AddProductDependencyFormValues {
  dependsOnProductId?: string
  strength?: DependencyStrength
  interactionStyles?: InteractionStyle[]
  description?: string
  startsOn?: Dayjs
}

/**
 * Records that a product depends on another.
 *
 * The picker hides the product's own subtree and shows its ancestors without letting them be chosen: a
 * product and anything above or below it is composition, which the tree already records. Ancestors stay
 * visible because hiding one would hide the branches beneath it, where legal choices sit. The API enforces
 * the same rule; this is about not offering it.
 */
const AddProductDependencyForm = ({
  product,
  onFormComplete,
  onFormCancel,
}: AddProductDependencyFormProps) => {
  const messageApi = useMessage()

  const [addProductDependency] = useAddProductDependencyMutation()
  const { data: products } = useGetProductsQuery(undefined)

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<AddProductDependencyFormValues>({
      onSubmit: async (values: AddProductDependencyFormValues, form) => {
        try {
          const request = {
            dependsOnProductId: values.dependsOnProductId,
            strength: values.strength,
            interactionStyles: values.interactionStyles,
            description: values.description,
            startsOn: values.startsOn?.format('YYYY-MM-DD'),
          } as AddProductDependencyRequest

          const response = await addProductDependency({
            productId: product.id,
            request,
          })
          if (response.error) throw response.error

          messageApi.success('Dependency added.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while adding the dependency. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while adding the dependency. Please try again.',
      permission: 'Permissions.Products.Update',
    })

  return (
    <Modal
      title="Add Dependency"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Add"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="add-product-dependency-form"
      >
        <Item
          name="dependsOnProductId"
          label="Depends On"
          rules={[
            { required: true, message: 'Select the product it depends on' },
          ]}
          extra="Record the most specific product you know — the service, not the platform it belongs to."
        >
          <ProductTreeSelect
            excludeSubtreeOf={product.id}
            unselectableIds={ancestorIdsOf(products ?? [], product.id)}
            placeholder="Select a product"
          />
        </Item>
        <Item
          name="strength"
          label="Strength"
          rules={[{ required: true, message: 'Choose how it depends on it' }]}
        >
          <DependencyStrengthRadio />
        </Item>
        <Item
          name="interactionStyles"
          label="Interaction"
          extra="How these products talk. Leaving this empty records nothing, not that there is neither."
        >
          <InteractionStyleCheckboxes />
        </Item>
        <Item
          name="description"
          label="Description"
          rules={[
            {
              max: 1024,
              message: 'Description cannot be longer than 1024 characters',
            },
          ]}
          extra="What it relies on the product for."
        >
          <TextArea autoSize={{ minRows: 2 }} showCount maxLength={1024} />
        </Item>
        <Item
          name="startsOn"
          label="Started"
          extra="Leave empty to start it today. Backdate it if the dependency is older."
        >
          <DatePicker
            style={{ width: '100%' }}
            disabledDate={(current) => current.isAfter(dayjs(), 'day')}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default AddProductDependencyForm
