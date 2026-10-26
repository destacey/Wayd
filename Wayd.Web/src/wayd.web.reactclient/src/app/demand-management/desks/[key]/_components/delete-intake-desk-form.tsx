'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { IntakeDeskDto } from '@/src/services/wayd-api'
import { useDeleteIntakeDeskMutation } from '@/src/store/features/demand-management/demand-management-api'
import { toApiError } from '@/src/utils'
import { Modal, Typography } from 'antd'
import { FC } from 'react'

const { Paragraph } = Typography

export interface DeleteIntakeDeskFormProps {
  desk: IntakeDeskDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Deletes a desk with no requests on it. Offered where the desk's `canDelete` says so: the Demand
 * Management Admin, while the desk is empty.
 */
const DeleteIntakeDeskForm: FC<DeleteIntakeDeskFormProps> = ({
  desk,
  onFormComplete,
  onFormCancel,
}) => {
  const messageApi = useMessage()
  const [deleteDesk] = useDeleteIntakeDeskMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        await deleteDesk(desk.id).unwrap()
        messageApi.success('Desk deleted.')
        return true
      } catch (error) {
        messageApi.error(
          toApiError(error).detail ?? 'Failed to delete the desk.',
        )
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: 'Failed to delete the desk.',
  })

  return (
    <Modal
      title={`Delete ${desk.name}?`}
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Paragraph>
        The desk and its members are removed. This cannot be undone.
      </Paragraph>
    </Modal>
  )
}

export default DeleteIntakeDeskForm
