'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { ScoringModelDetailsDto } from '@/src/services/wayd-api'
import {
  useActivateScoringModelMutation,
  useArchiveScoringModelMutation,
} from '@/src/store/features/scoring/scoring-models-api'
import { Modal, Space } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'

export enum ScoringModelStateAction {
  Activate = 'Activate',
  Archive = 'Archive',
}

export interface ChangeScoringModelStateFormProps {
  scoringModel: ScoringModelDetailsDto
  stateAction: ScoringModelStateAction
  onFormComplete: () => void
  onFormCancel: () => void
}

const ChangeScoringModelStateForm = ({
  scoringModel,
  stateAction,
  onFormComplete,
  onFormCancel,
}: ChangeScoringModelStateFormProps) => {
  const messageApi = useMessage()

  const [activateScoringModelMutation] = useActivateScoringModelMutation()
  const [archiveScoringModelMutation] = useArchiveScoringModelMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        let response = null
        if (stateAction === ScoringModelStateAction.Activate) {
          response = await activateScoringModelMutation(scoringModel.id)
        } else if (stateAction === ScoringModelStateAction.Archive) {
          response = await archiveScoringModelMutation(scoringModel.id)
        }

        if (response?.error) {
          throw response.error
        }

        const pastTense =
          stateAction === ScoringModelStateAction.Activate
            ? 'activated'
            : 'archived'
        messageApi.success(`Successfully ${pastTense} scoring model.`)
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        const gerund =
          stateAction === ScoringModelStateAction.Activate
            ? 'activating'
            : 'archiving'
        messageApi.error(
          apiError.detail ??
            `An unexpected error occurred while ${gerund} the scoring model.`,
        )
        console.log(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: `An unexpected error occurred while ${stateAction}ing the scoring model.`,
    permission: 'Permissions.ScoringModels.Update',
  })

  return (
    <Modal
      title={`Are you sure you want to ${stateAction} this Scoring Model?`}
      open={isOpen}
      onOk={handleOk}
      okText={stateAction}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Space vertical>
        <div>
          {scoringModel?.key} - {scoringModel?.name}
        </div>
        {stateAction === ScoringModelStateAction.Activate
          ? 'Activating locks the model so it can no longer be edited.'
          : 'This action cannot be undone.'}
      </Space>
    </Modal>
  )
}

export default ChangeScoringModelStateForm
