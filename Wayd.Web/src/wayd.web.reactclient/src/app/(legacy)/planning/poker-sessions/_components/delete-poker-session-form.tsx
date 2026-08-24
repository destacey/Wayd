'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useDeletePokerSessionMutation } from '@/src/store/features/planning/poker-sessions-api'
import { Modal } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'

export interface DeletePokerSessionFormProps {
  session: { id: string; key: number; name: string }
  onFormComplete: () => void
  onFormCancel: () => void
}

const DeletePokerSessionForm = ({
  session,
  onFormComplete,
  onFormCancel,
}: DeletePokerSessionFormProps) => {
  const messageApi = useMessage()

  const [deletePokerSession] = useDeletePokerSessionMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deletePokerSession(session.id)
        if (response.error) {
          throw response.error
        }
        messageApi.success('Successfully deleted poker session.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An unexpected error occurred while deleting the poker session.',
        )
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    permission: 'Permissions.PokerSessions.Delete',
  })

  return (
    <Modal
      title="Are you sure you want to delete this poker session?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {session?.key} - {session?.name}
    </Modal>
  )
}

export default DeletePokerSessionForm
