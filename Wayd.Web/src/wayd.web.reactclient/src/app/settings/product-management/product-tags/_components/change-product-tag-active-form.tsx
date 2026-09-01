'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { ProductTagOptionDto } from '@/src/services/wayd-api'
import { useSetProductTagActiveMutation } from '@/src/store/features/product-management/product-tag-categories-api'
import { Modal } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'

export interface ChangeProductTagActiveFormProps {
  categoryId: string
  tag: ProductTagOptionDto
  /** True puts the tag back into use, false retires it. */
  isActive: boolean
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Retires a tag from new use, or puts it back.
 *
 * The product count is spelled out when retiring, because that is the number
 * the decision turns on: those products keep the tag, and the confirmation says
 * so rather than leaving the reader to assume it strips them.
 */
const ChangeProductTagActiveForm = ({
  categoryId,
  tag,
  isActive,
  onFormComplete,
  onFormCancel,
}: ChangeProductTagActiveFormProps) => {
  const messageApi = useMessage()

  const [setProductTagActive] = useSetProductTagActiveMutation()

  const gerund = isActive ? 'activating' : 'deactivating'

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await setProductTagActive({
          categoryId,
          tagId: tag.id,
          isActive,
        })
        if (response.error) {
          throw response.error
        }
        messageApi.success(
          `Successfully ${isActive ? 'activated' : 'deactivated'} tag.`,
        )
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            `An unexpected error occurred while ${gerund} the tag.`,
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: `An unexpected error occurred while ${gerund} the tag.`,
    permission: 'Permissions.ProductTagCategories.Update',
  })

  return (
    <Modal
      title={`${isActive ? 'Activate' : 'Deactivate'} this tag?`}
      open={isOpen}
      onOk={handleOk}
      okText={isActive ? 'Activate' : 'Deactivate'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {tag.name}
      {!isActive && (
        <p>
          {tag.productCount > 0
            ? `${tag.productCount} product(s) carry this tag and keep it. It can no longer be applied to anything new.`
            : 'The tag can no longer be applied to a product.'}
        </p>
      )}
    </Modal>
  )
}

export default ChangeProductTagActiveForm
