'use client'

import { isApiError } from '@/src/utils'
import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import {
  useDeleteProjectTaskMutation,
  useGetProjectTaskQuery,
} from '@/src/store/features/ppm/project-tasks-api'
import { Modal } from 'antd'

export interface DeleteProjectTaskFormProps {
  projectIdOrKey: string
  taskIdOrKey: string
  onFormComplete: () => void
  onFormCancel: () => void
}

const DeleteProjectTaskForm = ({
  projectIdOrKey,
  taskIdOrKey,
  onFormComplete,
  onFormCancel,
}: DeleteProjectTaskFormProps) => {
  const messageApi = useMessage()

  const [deleteProjectTaskMutation] = useDeleteProjectTaskMutation()

  const { data: taskData, isLoading } = useGetProjectTaskQuery({
    projectIdOrKey,
    taskIdOrKey,
  })

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      if (!taskData) return false

      try {
        const response = await deleteProjectTaskMutation({
          projectIdOrKey,
          id: taskData.id,
        })

        if (response.error) throw response.error

        messageApi.success('Successfully deleted task.')
        return true
      } catch (error) {
        console.log('delete task error', error)
        messageApi.error(
          (isApiError(error) ? error.detail : undefined) ??
            'An unexpected error occurred while deleting the task.',
        )
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: 'An unexpected error occurred while deleting the task.',
    permission: 'Permissions.Projects.Update',
  })

  if (isLoading) {
    return null
  }

  return (
    <Modal
      title="Are you sure you want to delete this task?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {taskData?.key} - {taskData?.name}
    </Modal>
  )
}

export default DeleteProjectTaskForm
