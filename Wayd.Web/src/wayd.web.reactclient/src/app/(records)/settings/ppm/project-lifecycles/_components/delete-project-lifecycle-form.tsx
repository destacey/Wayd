'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { ProjectLifecycleDetailsDto } from '@/src/services/wayd-api'
import { useDeleteProjectLifecycleMutation } from '@/src/store/features/ppm/project-lifecycles-api'
import { Modal } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'

export interface DeleteProjectLifecycleFormProps {
  lifecycle: ProjectLifecycleDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

const DeleteProjectLifecycleForm = ({
  lifecycle,
  onFormComplete,
  onFormCancel,
}: DeleteProjectLifecycleFormProps) => {
  const messageApi = useMessage()

  const [deleteProjectLifecycle] = useDeleteProjectLifecycleMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteProjectLifecycle(lifecycle.id)
        if (response.error) {
          throw response.error
        }
        messageApi.success('Successfully deleted project lifecycle.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An unexpected error occurred while deleting the project lifecycle.',
        )
        console.log(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An unexpected error occurred while deleting the project lifecycle.',
    permission: 'Permissions.ProjectLifecycles.Delete',
  })

  return (
    <Modal
      title="Are you sure you want to delete this Project Lifecycle?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {lifecycle?.key} - {lifecycle?.name}
    </Modal>
  )
}

export default DeleteProjectLifecycleForm
