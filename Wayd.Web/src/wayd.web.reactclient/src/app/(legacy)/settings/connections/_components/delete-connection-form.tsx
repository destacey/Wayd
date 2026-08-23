'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { ConnectionDetailsDto } from '@/src/services/wayd-api'
import { useDeleteConnectionMutation } from '@/src/store/features/app-integration/connections-api'
import { Descriptions, Modal } from 'antd'
import { useConfirmModal } from '@/src/hooks'

const { Item } = Descriptions

interface DeleteConnectionFormProps {
  connection: ConnectionDetailsDto
  onFormSave: () => void
  onFormCancel: () => void
}

const DeleteConnectionForm = ({
  connection,
  onFormSave,
  onFormCancel,
}: DeleteConnectionFormProps) => {
  const messageApi = useMessage()
  const connectorName = connection.connector?.name ?? 'connection'

  const [deleteConnectionMutation] = useDeleteConnectionMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteConnectionMutation(connection.id)
        if (response.error) {
          throw response.error
        }
        messageApi.success(`Successfully deleted ${connectorName} connection.`)
        return true
      } catch (error) {
        messageApi.error(
          `An unexpected error occurred while deleting the ${connectorName} connection.`,
        )
        console.log(error)
        return false
      }
    },
    onComplete: onFormSave,
    onCancel: onFormCancel,
    errorMessage: `An unexpected error occurred while deleting the ${connectorName} connection.`,
    permission: 'Permissions.Connections.Delete',
  })

  return (
    <Modal
      title={`Are you sure you want to delete this ${connectorName} connection?`}
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Descriptions size="small" column={1}>
        <Item label="Name">{connection?.name}</Item>
      </Descriptions>
    </Modal>
  )
}

export default DeleteConnectionForm
