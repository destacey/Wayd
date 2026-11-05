'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { RiskDetailsDto } from '@/src/services/wayd-api'
import { useDeleteRiskMutation } from '@/src/store/features/planning/risks-api'
import { Modal } from 'antd'
import { isApiError, type ApiError } from '@/src/utils'

export interface DeleteRiskFormProps {
  risk: RiskDetailsDto
  onFormSave: () => void
  onFormCancel: () => void
}

const DeleteRiskForm = ({
  risk,
  onFormSave,
  onFormCancel,
}: DeleteRiskFormProps) => {
  const messageApi = useMessage()

  const [deleteRisk] = useDeleteRiskMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteRisk(risk.id)
        if (response.error) {
          throw response.error
        }
        messageApi.success('Successfully deleted risk.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An unexpected error occurred while deleting the risk.',
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormSave,
    onCancel: onFormCancel,
    errorMessage: 'An unexpected error occurred while deleting the risk.',
    permission: 'Permissions.Risks.Delete',
  })

  return (
    <Modal
      title="Are you sure you want to delete this risk?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {risk.key} - {risk.summary}
    </Modal>
  )
}

export default DeleteRiskForm
