'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { StrategicInitiativeDetailsDto } from '@/src/services/wayd-api'
import { useDeleteStrategicInitiativeMutation } from '@/src/store/features/ppm/strategic-initiatives-api'
import { Modal } from 'antd'
import { isApiError, type ApiError } from '@/src/utils'

export interface DeleteStrategicInitiativeFormProps {
  strategicInitiative: StrategicInitiativeDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

const DeleteStrategicInitiativeForm = ({
  strategicInitiative,
  onFormComplete,
  onFormCancel,
}: DeleteStrategicInitiativeFormProps) => {
  const messageApi = useMessage()

  const [deleteStrategicInitiativeMutation] =
    useDeleteStrategicInitiativeMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteStrategicInitiativeMutation(
          strategicInitiative.id,
        )
        if (response.error) throw response.error

        messageApi.success('Successfully deleted strategic initiative.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An unexpected error occurred while deleting the strategic initiative.',
        )
        console.log(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An unexpected error occurred while deleting the strategic initiative.',
    permission: 'Permissions.StrategicInitiatives.Delete',
  })

  return (
    <Modal
      title="Are you sure you want to delete this Strategic Initiative?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      {strategicInitiative?.key} - {strategicInitiative?.name}
    </Modal>
  )
}

export default DeleteStrategicInitiativeForm
