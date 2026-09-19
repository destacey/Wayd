'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { ReleasePackageDto } from '@/src/services/wayd-api'
import { useDeleteReleasePackageMutation } from '@/src/store/features/product-management/release-packages-api'
import { isApiError, type ApiError } from '@/src/utils'
import { Modal, Typography } from 'antd'

const { Paragraph, Text } = Typography

export interface DeleteReleasePackageFormProps {
  releasePackage: ReleasePackageDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/** Deletes a package and what hangs off it. */
const DeleteReleasePackageForm = ({
  releasePackage,
  onFormComplete,
  onFormCancel,
}: DeleteReleasePackageFormProps) => {
  const messageApi = useMessage()

  const [deleteReleasePackage] = useDeleteReleasePackageMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteReleasePackage({
          id: releasePackage.id,
          cacheKey: releasePackage.key,
        })
        if (response.error) throw response.error

        messageApi.success('Package deleted.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An error occurred while deleting the package. Please try again.',
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An error occurred while deleting the package. Please try again.',
    permission: 'Permissions.Delivery.Delete',
  })

  return (
    <Modal
      title="Delete Package"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Paragraph>
        Delete package <Text strong>{releasePackage.version}</Text>? This cannot
        be undone.
      </Paragraph>
      <Paragraph type="secondary">
        Its manifest, status history and every deployment of it are deleted too.
        The versions it names are kept. A package that any release lists cannot
        be deleted. Withdraw it instead to keep all of that.
      </Paragraph>
    </Modal>
  )
}

export default DeleteReleasePackageForm
