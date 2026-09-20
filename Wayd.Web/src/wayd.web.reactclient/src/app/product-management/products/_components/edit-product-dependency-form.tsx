'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  InteractionStyle,
  ProductDependencyDto,
  UpdateProductDependencyRequest,
} from '@/src/services/wayd-api'
import { useUpdateProductDependencyMutation } from '@/src/store/features/product-management/products-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { InteractionStyleCheckboxes } from './interaction-style'

const { Item } = Form
const { TextArea } = Input

export interface EditProductDependencyFormProps {
  dependency: ProductDependencyDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface EditProductDependencyFormValues {
  description?: string
  interactionStyles?: InteractionStyle[]
}

/**
 * Rewords what a dependency is for, and records how the products talk where nobody had.
 *
 * Both describe the dependency rather than asserting when it held, which is why they are edited in place
 * while the strength changes by ending the link and starting another. Styles already recorded are shown
 * disabled: replacing them is a change of terms and has to be dated, so it belongs to the terms form.
 */
const EditProductDependencyForm = ({
  dependency,
  onFormComplete,
  onFormCancel,
}: EditProductDependencyFormProps) => {
  const messageApi = useMessage()

  const [updateProductDependency] = useUpdateProductDependencyMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditProductDependencyFormValues>({
      onSubmit: async (values: EditProductDependencyFormValues, form) => {
        try {
          const request = {
            description: values.description,
            interactionStyles: values.interactionStyles,
          } as UpdateProductDependencyRequest

          const response = await updateProductDependency({
            productId: dependency.product.id,
            dependencyId: dependency.id,
            dependsOnProductId: dependency.dependsOnProduct.id,
            request,
          })
          if (response.error) throw response.error

          messageApi.success('Dependency updated.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while updating the dependency. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the dependency. Please try again.',
      permission: 'Permissions.Products.Update',
    })

  const alreadyRecorded = !!dependency.interactionStyles?.length

  return (
    <Modal
      title="Edit Dependency"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="edit-product-dependency-form"
        initialValues={{
          description: dependency.description,
          interactionStyles: dependency.interactionStyles ?? [],
        }}
      >
        <Item
          name="description"
          label={`${dependency.product.name} depends on ${dependency.dependsOnProduct.name} for`}
          rules={[
            {
              max: 1024,
              message: 'Description cannot be longer than 1024 characters',
            },
          ]}
        >
          <TextArea autoSize={{ minRows: 2 }} showCount maxLength={1024} />
        </Item>
        <Item
          name="interactionStyles"
          label="Interaction"
          extra={
            alreadyRecorded
              ? 'Already recorded. Moving between calling and events is a change of terms, which is dated — use Change Terms.'
              : 'How these products talk. Recording it here keeps one period, because writing down what was always true changes nothing.'
          }
        >
          <InteractionStyleCheckboxes disabled={alreadyRecorded} />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditProductDependencyForm
