'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { ProgramDetailsDto } from '@/src/services/wayd-api'
import { useDeleteProgramMutation } from '@/src/store/features/ppm/programs-api'
import { Modal } from 'antd'
import { isApiError, type ApiError } from '@/src/utils'

export interface DeleteProgramFormProps {
  program: ProgramDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

const DeleteProgramForm = ({
  program,
  onFormComplete,
  onFormCancel,
}: DeleteProgramFormProps) => {
  const messageApi = useMessage()

  const [deleteProgramMutation] = useDeleteProgramMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteProgramMutation(program.id)
        if (response.error) throw response.error

        messageApi.success('Successfully deleted Program.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An unexpected error occurred while deleting the program.',
        )
        console.log(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An unexpected error occurred while deleting the program.',
    permission: 'Permissions.Programs.Delete',
  })

  return (
    <Modal
      title="Are you sure you want to delete this Program?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      {program?.key} - {program?.name}
    </Modal>
  )
}

export default DeleteProgramForm
