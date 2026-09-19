'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { ReleaseDto } from '@/src/services/wayd-api'
import { useDeleteReleaseMutation } from '@/src/store/features/product-management/releases-api'
import { isApiError, type ApiError } from '@/src/utils'
import { Alert, Modal, Typography } from 'antd'

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
        Delete release <Text strong>{release.version}</Text>?
      </Paragraph>
      <Alert
        type="warning"
        showIcon
        title="This cannot be undone"
        description="Its status history and its list of contents are deleted too. The versions and packages it listed are kept."
      />
      <Paragraph type="secondary" style={{ marginTop: 12, marginBottom: 0 }}>
        Withdraw it instead to keep the record of what was announced.
      </Paragraph>
    </Modal>
  )
}

export default DeleteReleaseForm
