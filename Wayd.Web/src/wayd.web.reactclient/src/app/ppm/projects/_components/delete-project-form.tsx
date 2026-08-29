'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { ProjectDetailsDto } from '@/src/services/wayd-api'
import { useDeleteProjectMutation } from '@/src/store/features/ppm/projects-api'
import { Modal } from 'antd'
import { isApiError, type ApiError } from '@/src/utils'

export interface DeleteProjectFormProps {
  project: ProjectDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

const DeleteProjectForm = ({
  project,
  onFormComplete,
  onFormCancel,
}: DeleteProjectFormProps) => {
  const messageApi = useMessage()

  const [deleteProjectMutation] = useDeleteProjectMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteProjectMutation(project.id)
        if (response.error) throw response.error

        messageApi.success('Successfully deleted Project.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An unexpected error occurred while deleting the project.',
        )
        console.log(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An unexpected error occurred while deleting the project.',
    permission: 'Permissions.Projects.Delete',
  })

  return (
    <Modal
      title="Are you sure you want to delete this Project?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      {project?.key} - {project?.name}
    </Modal>
  )
}

export default DeleteProjectForm
