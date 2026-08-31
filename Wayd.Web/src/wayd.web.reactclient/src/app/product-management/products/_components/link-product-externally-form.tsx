'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import {
  LinkProductExternallyRequest,
  ProductDto,
} from '@/src/services/wayd-api'
import { useLinkProductExternallyMutation } from '@/src/store/features/product-management/products-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'

const { Item } = Form

export interface LinkProductExternallyFormProps {
  product: ProductDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface LinkProductExternallyFormValues {
  externalId?: string
}

/**
 * Records where a product lives in the system that owns it.
 *
 * Its own action rather than a field on Edit, all the way down to its own
 * endpoint: linking a product to a repository or a pipeline is a different
 * intent from renaming it, and the value exists so a later automated feed can be
 * matched against hand-curated products rather than re-authored.
 */
const LinkProductExternallyForm = ({
  product,
  onFormComplete,
  onFormCancel,
}: LinkProductExternallyFormProps) => {
  const messageApi = useMessage()

  const [linkProductExternally] = useLinkProductExternallyMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<LinkProductExternallyFormValues>({
      onSubmit: async (values: LinkProductExternallyFormValues, form) => {
        try {
          const request = {
            id: product.id,
            externalId: values.externalId,
          } as LinkProductExternallyRequest

          const response = await linkProductExternally({
            id: product.id,
            request,
          })
          if (response.error) throw response.error

          messageApi.success('External link updated successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while updating the external link. Please try again.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage:
        'An error occurred while updating the external link. Please try again.',
      permission: 'Permissions.Products.Update',
    })

  return (
    <Modal
      title="Link Externally"
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
        name="link-product-externally-form"
        initialValues={{ externalId: product.externalId }}
      >
        <Item
          name="externalId"
          label="External Id"
          rules={[{ max: 256 }]}
          extra="Its identifier in the system that owns it — a repository, a pipeline, a registry package. Clear it to unlink."
        >
          <Input showCount maxLength={256} allowClear />
        </Item>
      </Form>
    </Modal>
  )
}

export default LinkProductExternallyForm
