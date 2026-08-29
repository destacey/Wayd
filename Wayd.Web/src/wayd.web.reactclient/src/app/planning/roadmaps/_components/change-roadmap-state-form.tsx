'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { RoadmapDetailsDto } from '@/src/services/wayd-api'
import {
  useActivateRoadmapMutation,
  useArchiveRoadmapMutation,
} from '@/src/store/features/planning/roadmaps-api'
import { Modal, Space } from 'antd'
import { isApiError, type ApiError } from '@/src/utils'

export enum RoadmapStateAction {
  Activate = 'Activate',
  Archive = 'Archive',
}

export interface ChangeRoadmapStateFormProps {
  roadmap: RoadmapDetailsDto
  stateAction: RoadmapStateAction
  onFormComplete: () => void
  onFormCancel: () => void
}

const ChangeRoadmapStateForm = ({
  roadmap,
  stateAction,
  onFormComplete,
  onFormCancel,
}: ChangeRoadmapStateFormProps) => {
  const messageApi = useMessage()

  const [activateRoadmapMutation] = useActivateRoadmapMutation()
  const [archiveRoadmapMutation] = useArchiveRoadmapMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        let response = null
        if (stateAction === RoadmapStateAction.Activate) {
          response = await activateRoadmapMutation({
            id: roadmap.id,
            cacheKey: roadmap.key,
          })
        } else if (stateAction === RoadmapStateAction.Archive) {
          response = await archiveRoadmapMutation({
            id: roadmap.id,
            cacheKey: roadmap.key,
          })
        }

        if (response?.error) {
          throw response.error
        }

        messageApi.success(
          `Successfully ${stateAction.toLowerCase()}d Roadmap.`,
        )
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            `An unexpected error occurred while ${stateAction.toLowerCase()}ing the roadmap.`,
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: `An unexpected error occurred while ${stateAction.toLowerCase()}ing the roadmap.`,
    permission: 'Permissions.Roadmaps.Update',
  })

  return (
    <Modal
      title={`Are you sure you want to ${stateAction.toLowerCase()} this Roadmap?`}
      open={isOpen}
      onOk={handleOk}
      okText={stateAction}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Space direction="vertical">
        <div>
          {roadmap?.key} - {roadmap?.name}
        </div>
      </Space>
    </Modal>
  )
}

export default ChangeRoadmapStateForm
