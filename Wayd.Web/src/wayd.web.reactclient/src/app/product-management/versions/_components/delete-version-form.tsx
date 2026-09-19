'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { VersionDto } from '@/src/services/wayd-api'
import { useDeleteVersionMutation } from '@/src/store/features/product-management/versions-api'
import { isApiError, type ApiError } from '@/src/utils'
import { Modal, Typography } from 'antd'

const { Paragraph, Text } = Typography

export interface DeleteVersionFormProps {
  version: VersionDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/** Deletes a version and every deployment of it. */
const DeleteVersionForm = ({
  version,
  onFormComplete,
  onFormCancel,
}: DeleteVersionFormProps) => {
  const messageApi = useMessage()

  const [deleteVersion] = useDeleteVersionMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteVersion(version.id)
        if (response.error) throw response.error

        messageApi.success('Version deleted.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An error occurred while deleting the version. Please try again.',
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An error occurred while deleting the version. Please try again.',
    permission: 'Permissions.Delivery.Delete',
  })

  return (
    <Modal
      title="Delete Version"
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
        Delete version <Text strong>{version.number}</Text>? This cannot be
        undone.
      </Paragraph>
      <Paragraph type="secondary">
        Its status history and every deployment of it are deleted too. A version
        that a release lists or a package manifest names cannot be deleted.
        Withdraw it instead to keep all of that.
      </Paragraph>
    </Modal>
  )
}

export default DeleteVersionForm
