import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { PlanningIntervalObjectiveDetailsDto } from '@/src/services/wayd-api'
import { useDeletePlanningIntervalObjectiveMutation } from '@/src/store/features/planning/planning-interval-api'
import { Modal } from 'antd'
import { isApiError, type ApiError } from '@/src/utils'

export interface DeletePlanningIntervalObjectiveFormProps {
  objective: PlanningIntervalObjectiveDetailsDto
  onFormSave: () => void
  onFormCancel: () => void
}

const DeletePlanningIntervalObjectiveForm = ({
  objective,
  onFormSave,
  onFormCancel,
}: DeletePlanningIntervalObjectiveFormProps) => {
  const messageApi = useMessage()

  const [deleteObjective] = useDeletePlanningIntervalObjectiveMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteObjective({
          planningIntervalId: objective.planningInterval.id,
          planningIntervalKey: objective.planningInterval.key,
          objectiveId: objective.id,
          objectiveKey: objective.key,
          teamId: objective.team.id,
        })
        if (response.error) {
          throw response.error
        }
        messageApi.success('Successfully deleted PI objective.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An unexpected error occurred while deleting the objective.',
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormSave,
    onCancel: onFormCancel,
    errorMessage:
      'An unexpected error occurred while deleting the objective.',
    permission: 'Permissions.PlanningIntervalObjectives.Manage',
  })

  return (
    <Modal
      title="Are you sure you want to delete this PI Objective?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {objective?.key} - {objective?.name}
    </Modal>
  )
}

export default DeletePlanningIntervalObjectiveForm
