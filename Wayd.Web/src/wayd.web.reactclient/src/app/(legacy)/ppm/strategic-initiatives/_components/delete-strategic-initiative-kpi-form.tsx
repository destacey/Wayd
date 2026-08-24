'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { StrategicInitiativeKpiListDto } from '@/src/services/wayd-api'
import { useDeleteStrategicInitiativeKpiMutation } from '@/src/store/features/ppm/strategic-initiatives-api'
import { Modal } from 'antd'
import { isApiError, type ApiError } from '@/src/utils'

export interface DeleteStrategicInitiativeKpiFormProps {
  strategicInitiativeId: string
  kpi: StrategicInitiativeKpiListDto
  onFormComplete: () => void
  onFormCancel: () => void
}

const DeleteStrategicInitiativeKpiForm = ({
  strategicInitiativeId,
  kpi,
  onFormComplete,
  onFormCancel,
}: DeleteStrategicInitiativeKpiFormProps) => {
  const messageApi = useMessage()

  const [deleteKpiMutation] = useDeleteStrategicInitiativeKpiMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteKpiMutation({
          strategicInitiativeId,
          kpiId: kpi.id,
        })
        if (response.error) throw response.error

        messageApi.success('Successfully deleted KPI.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An unexpected error occurred while deleting the KPI.',
        )
        console.log(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: 'An unexpected error occurred while deleting the KPI.',
    permission: 'Permissions.StrategicInitiatives.Update',
  })

  return (
    <Modal
      title="Are you sure you want to delete this KPI?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      {kpi?.key} - {kpi?.name}
    </Modal>
  )
}

export default DeleteStrategicInitiativeKpiForm
