'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { ScoringModelDetailsDto } from '@/src/services/wayd-api'
import { useDeleteScoringModelMutation } from '@/src/store/features/scoring/scoring-models-api'
import { Modal } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'

export interface DeleteScoringModelFormProps {
  scoringModel: ScoringModelDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

const DeleteScoringModelForm = ({
  scoringModel,
  onFormComplete,
  onFormCancel,
}: DeleteScoringModelFormProps) => {
  const messageApi = useMessage()

  const [deleteScoringModel] = useDeleteScoringModelMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteScoringModel(scoringModel.id)
        if (response.error) {
          throw response.error
        }
        messageApi.success('Successfully deleted scoring model.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An unexpected error occurred while deleting the scoring model.',
        )
        console.log(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An unexpected error occurred while deleting the scoring model.',
    permission: 'Permissions.ScoringModels.Delete',
  })

  return (
    <Modal
      title="Are you sure you want to delete this Scoring Model?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {scoringModel?.key} - {scoringModel?.name}
    </Modal>
  )
}

export default DeleteScoringModelForm
