'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useArchiveStoryMapMutation } from '@/src/store/features/planning/story-maps-api'
import { Modal } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'

export interface ArchiveStoryMapFormProps {
  storyMap: { id: string; key: number; name: string }
  onFormComplete: () => void
  onFormCancel: () => void
}

const ArchiveStoryMapForm = ({
  storyMap,
  onFormComplete,
  onFormCancel,
}: ArchiveStoryMapFormProps) => {
  const messageApi = useMessage()

  const [archiveStoryMap] = useArchiveStoryMapMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await archiveStoryMap({ id: storyMap.id })
        if (response.error) {
          throw response.error
        }
        messageApi.success('Successfully archived story map.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An unexpected error occurred while archiving the story map.',
        )
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    permission: 'Permissions.StoryMaps.Update',
  })

  return (
    <Modal
      title="Are you sure you want to archive this story map?"
      open={isOpen}
      onOk={handleOk}
      okText="Archive"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {storyMap?.key} - {storyMap?.name}
    </Modal>
  )
}

export default ArchiveStoryMapForm
