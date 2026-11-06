'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { ProjectDeliverableDto } from '@/src/services/wayd-api'
import { useDeleteProjectDeliverableMutation } from '@/src/store/features/ppm/project-deliverables-api'
import { toApiError } from '@/src/utils'
import { Modal } from 'antd'
import { deliverableErrorMessage } from './project-deliverable-errors'

export interface DeleteProjectDeliverableFormProps {
  projectId: string
  projectRevision: number
  deliverable: ProjectDeliverableDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Deletes a deliverable added by mistake. Offered only while the project and
 * the deliverable are both unstarted; after that, descoping keeps it and why.
 */
const DeleteProjectDeliverableForm = ({
  projectId,
  projectRevision,
  deliverable,
  onFormComplete,
  onFormCancel,
}: DeleteProjectDeliverableFormProps) => {
  const messageApi = useMessage()
  const [deleteDeliverable] = useDeleteProjectDeliverableMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        await deleteDeliverable({
          projectId,
          deliverableId: deliverable.id,
          expectedRevision: projectRevision,
        }).unwrap()
        messageApi.success('Deliverable deleted.')
        return true
      } catch (error) {
        messageApi.error(
          deliverableErrorMessage(
            toApiError(error),
            'Failed to delete the deliverable.',
          ),
        )
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: 'Failed to delete the deliverable.',
    permission: 'Permissions.Projects.Update',
  })

  return (
    <Modal
      title="Delete this deliverable?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {deliverable.name}
    </Modal>
  )
}

export default DeleteProjectDeliverableForm
