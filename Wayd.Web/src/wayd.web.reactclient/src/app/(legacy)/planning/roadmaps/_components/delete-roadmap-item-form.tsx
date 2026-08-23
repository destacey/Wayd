import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import {
  useDeleteRoadmapItemMutation,
  useGetRoadmapItemQuery,
} from '@/src/store/features/planning/roadmaps-api'
import { Modal } from 'antd'
import { useEffect } from 'react'
import { isApiError } from '@/src/utils'

export interface DeleteRoadmapItemFormProps {
  roadmapId: string
  roadmapItemId: string
  onFormComplete: () => void
  onFormCancel: () => void
}

const DeleteRoadmapItemForm = ({
  roadmapId,
  roadmapItemId,
  onFormComplete,
  onFormCancel,
}: DeleteRoadmapItemFormProps) => {
  const messageApi = useMessage()

  const [deleteRoadmapItemMutation] = useDeleteRoadmapItemMutation()

  const {
    data: itemData,
    isLoading: itemDataIsLoading,
    error: itemDataError,
  } = useGetRoadmapItemQuery({
    roadmapId: roadmapId,
    itemId: roadmapItemId,
  })

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      if (!itemData) return false
      try {
        await deleteRoadmapItemMutation({
          roadmapId: itemData.roadmapId,
          itemId: itemData.id,
        })
        messageApi.success('Successfully deleted Roadmap Item.')
        return true
      } catch (error) {
        messageApi.error(
          'An unexpected error occurred while deleting the roadmap item.',
        )
        console.log(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An unexpected error occurred while deleting the roadmap item.',
    permission: 'Permissions.Roadmaps.Update',
  })

  // Query error display
  useEffect(() => {
    if (itemDataError) {
      messageApi.error(
        (isApiError(itemDataError) ? itemDataError.supportMessage : undefined) ??
          'An error occurred while loading roadmap item. Please try again.',
      )
    }
  }, [itemDataError, messageApi])

  return (
    <Modal
      title="Are you sure you want to delete this Roadmap Item?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      okButtonProps={{ disabled: !itemData }}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      {itemData?.name}
    </Modal>
  )
}

export default DeleteRoadmapItemForm
