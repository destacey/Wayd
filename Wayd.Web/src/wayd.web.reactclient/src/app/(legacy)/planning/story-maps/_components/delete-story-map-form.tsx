'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useDeleteStoryMapMutation } from '@/src/store/features/planning/story-maps-api'
import { Modal } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'

export interface DeleteStoryMapFormProps {
  storyMap: { id: string; key: number; name: string }
  onFormComplete: () => void
  onFormCancel: () => void
}

const DeleteStoryMapForm = ({
  storyMap,
  onFormComplete,
  onFormCancel,
}: DeleteStoryMapFormProps) => {
  const messageApi = useMessage()

  const [deleteStoryMap] = useDeleteStoryMapMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteStoryMap({ id: storyMap.id })
        if (response.error) {
          throw response.error
        }
        messageApi.success('Successfully deleted story map.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An unexpected error occurred while deleting the story map.',
        )
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    permission: 'Permissions.StoryMaps.Delete',
  })

  return (
    <Modal
      title="Are you sure you want to delete this story map?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {storyMap?.key} - {storyMap?.name}
    </Modal>
  )
}

export default DeleteStoryMapForm
