'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { RoadmapDetailsDto } from '@/src/services/wayd-api'
import { useDeleteRoadmapMutation } from '@/src/store/features/planning/roadmaps-api'
import { Modal } from 'antd'

export interface DeleteRoadmapFormProps {
  roadmap: RoadmapDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

const DeleteRoadmapForm = ({
  roadmap,
  onFormComplete,
  onFormCancel,
}: DeleteRoadmapFormProps) => {
  const messageApi = useMessage()

  const [deleteRoadmapMutation] = useDeleteRoadmapMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        await deleteRoadmapMutation(roadmap.id)
        // TODO: not working because the parent page is gone
        messageApi.success('Successfully deleted Roadmap.')
        return true
      } catch (error) {
        messageApi.error(
          'An unexpected error occurred while deleting the roadmap.',
        )
        console.log(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An unexpected error occurred while deleting the roadmap.',
    permission: 'Permissions.Roadmaps.Delete',
  })

  return (
    <Modal
      title="Are you sure you want to delete this Roadmap?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      {roadmap?.key} - {roadmap?.name}
    </Modal>
  )
}

export default DeleteRoadmapForm
