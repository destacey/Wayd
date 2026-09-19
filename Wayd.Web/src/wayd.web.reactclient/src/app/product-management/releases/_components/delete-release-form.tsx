'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { ReleaseDto } from '@/src/services/wayd-api'
import { useDeleteReleaseMutation } from '@/src/store/features/product-management/releases-api'
import { isApiError, type ApiError } from '@/src/utils'
import { Modal, Typography } from 'antd'

const { Paragraph, Text } = Typography

export interface DeleteReleaseFormProps {
  release: ReleaseDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/** Deletes a release, leaving the versions and packages it listed in place. */
const DeleteReleaseForm = ({
  release,
  onFormComplete,
  onFormCancel,
}: DeleteReleaseFormProps) => {
  const messageApi = useMessage()

  const [deleteRelease] = useDeleteReleaseMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteRelease(release.id)
        if (response.error) throw response.error

        messageApi.success('Release deleted.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An error occurred while deleting the release. Please try again.',
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An error occurred while deleting the release. Please try again.',
    permission: 'Permissions.Releases.Delete',
  })

  return (
    <Modal
      title="Delete Release"
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
        Delete release <Text strong>{release.version}</Text>? This cannot be
        undone.
      </Paragraph>
      <Paragraph type="secondary">
        Its status history and its list of contents are deleted too. The
        versions and packages it listed are kept. Withdraw it instead to keep
        the record of what was announced.
      </Paragraph>
    </Modal>
  )
}

export default DeleteReleaseForm
