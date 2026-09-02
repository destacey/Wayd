'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useSetProductTagCategoryActiveMutation } from '@/src/store/features/product-management/product-tag-categories-api'
import { Modal } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'
import { ProductTagCategoryActionTarget } from './types'

export interface ChangeProductTagCategoryActiveFormProps {
  category: ProductTagCategoryActionTarget
  /** True activates, false deactivates. */
  isActive: boolean
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Takes an axis out of use, or puts it back.
 *
 * Deactivation rather than deletion is the normal way an axis retires: products
 * already tagged along it keep their tags, so what was recorded stays true —
 * only new tagging stops. The confirmation says so, because "deactivate" on its
 * own reads like it might strip them.
 */
const ChangeProductTagCategoryActiveForm = ({
  category,
  isActive,
  onFormComplete,
  onFormCancel,
}: ChangeProductTagCategoryActiveFormProps) => {
  const messageApi = useMessage()

  const [setProductTagCategoryActive] =
    useSetProductTagCategoryActiveMutation()

  const gerund = isActive ? 'activating' : 'deactivating'

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await setProductTagCategoryActive({
          id: category.id,
          isActive,
        })
        if (response.error) {
          throw response.error
        }
        messageApi.success(
          `Successfully ${isActive ? 'activated' : 'deactivated'} tag category.`,
        )
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            `An unexpected error occurred while ${gerund} the tag category.`,
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: `An unexpected error occurred while ${gerund} the tag category.`,
    permission: 'Permissions.ProductTagCategories.Update',
  })

  return (
    <Modal
      title={`${isActive ? 'Activate' : 'Deactivate'} this tag category?`}
      open={isOpen}
      onOk={handleOk}
      okText={isActive ? 'Activate' : 'Deactivate'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {category.name}
      {!isActive && (
        <p>
          Products can no longer be tagged along this axis. Products already
          tagged keep their tags.
        </p>
      )}
    </Modal>
  )
}

export default ChangeProductTagCategoryActiveForm
