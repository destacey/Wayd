'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useDeleteEstimationScaleMutation } from '@/src/store/features/planning/estimation-scales-api'
import { Modal } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'

export interface DeleteEstimationScaleFormProps {
  estimationScale: { id: number; name: string }
  onFormComplete: () => void
  onFormCancel: () => void
}

const DeleteEstimationScaleForm = ({
  estimationScale,
  onFormComplete,
  onFormCancel,
}: DeleteEstimationScaleFormProps) => {
  const messageApi = useMessage()

  const [deleteEstimationScale] = useDeleteEstimationScaleMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteEstimationScale(estimationScale.id)
        if (response.error) {
          throw response.error
        }
        messageApi.success('Successfully deleted estimation scale.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An unexpected error occurred while deleting the estimation scale.',
        )
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    permission: 'Permissions.EstimationScales.Delete',
  })

  return (
    <Modal
      title="Are you sure you want to delete this estimation scale?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {estimationScale?.id} - {estimationScale?.name}
    </Modal>
  )
}

export default DeleteEstimationScaleForm
