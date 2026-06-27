'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { StrategicInitiativeDetailsDto } from '@/src/services/wayd-api'
import {
  useActivateStrategicInitiativeMutation,
  useApproveStrategicInitiativeMutation,
  useCancelStrategicInitiativeMutation,
  useCompleteStrategicInitiativeMutation,
} from '@/src/store/features/ppm/strategic-initiatives-api'
import { Alert, Modal, Space } from 'antd'
import { isApiError, type ApiError } from '@/src/utils'

export enum StrategicInitiativeStatusAction {
  Approve = 'Approve',
  Activate = 'Activate',
  Complete = 'Complete',
  Cancel = 'Cancel',
}

const statusActionToPastTense = (
  statusAction: StrategicInitiativeStatusAction,
) => {
  switch (statusAction) {
    case StrategicInitiativeStatusAction.Approve:
      return 'approved'
    case StrategicInitiativeStatusAction.Activate:
      return 'activated'
    case StrategicInitiativeStatusAction.Complete:
      return 'completed'
    case StrategicInitiativeStatusAction.Cancel:
      return 'cancelled'
    default:
      return statusAction
  }
}

const statusActionToPresentTense = (
  statusAction: StrategicInitiativeStatusAction,
) => {
  switch (statusAction) {
    case StrategicInitiativeStatusAction.Approve:
      return 'approving'
    case StrategicInitiativeStatusAction.Activate:
      return 'activating'
    case StrategicInitiativeStatusAction.Complete:
      return 'completing'
    case StrategicInitiativeStatusAction.Cancel:
      return 'cancelling'
    default:
      return statusAction
  }
}

export interface ChangeStrategicInitiativeStatusFormProps {
  strategicInitiative: StrategicInitiativeDetailsDto
  statusAction: StrategicInitiativeStatusAction
  onFormComplete: () => void
  onFormCancel: () => void
}

const ChangeStrategicInitiativeStatusForm = ({
  strategicInitiative,
  statusAction,
  onFormComplete,
  onFormCancel,
}: ChangeStrategicInitiativeStatusFormProps) => {
  const messageApi = useMessage()

  const [approveStrategicInitiativeMutation] =
    useApproveStrategicInitiativeMutation()
  const [activateStrategicInitiativeMutation] =
    useActivateStrategicInitiativeMutation()
  const [completeStrategicInitiativeMutation] =
    useCompleteStrategicInitiativeMutation()
  const [cancelStrategicInitiativeMutation] =
    useCancelStrategicInitiativeMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const request = {
          id: strategicInitiative.id,
          cacheKey: strategicInitiative.key,
        }
        let response = null
        if (statusAction === StrategicInitiativeStatusAction.Approve) {
          response = await approveStrategicInitiativeMutation(request)
        } else if (
          statusAction === StrategicInitiativeStatusAction.Activate
        ) {
          response = await activateStrategicInitiativeMutation(request)
        } else if (
          statusAction === StrategicInitiativeStatusAction.Complete
        ) {
          response = await completeStrategicInitiativeMutation(request)
        } else if (statusAction === StrategicInitiativeStatusAction.Cancel) {
          response = await cancelStrategicInitiativeMutation(request)
        }

        if (response?.error) throw response.error

        messageApi.success(
          `Successfully ${statusActionToPastTense(statusAction)} Strategic Initiative.`,
        )
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            `An unexpected error occurred while ${statusActionToPresentTense(statusAction)} the strategic initiative.`,
        )
        console.log(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: `An unexpected error occurred while ${statusActionToPresentTense(statusAction)} the strategic initiative.`,
    permission: 'Permissions.StrategicInitiatives.Update',
  })

  return (
    <Modal
      title={`Are you sure you want to ${statusAction} this Strategic Initiative?`}
      open={isOpen}
      onOk={handleOk}
      okText={statusAction}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Space vertical>
        <div>
          {strategicInitiative?.key} - {strategicInitiative?.name}
        </div>
        <Alert title="This action cannot be undone." type="warning" showIcon />
      </Space>
    </Modal>
  )
}

export default ChangeStrategicInitiativeStatusForm
