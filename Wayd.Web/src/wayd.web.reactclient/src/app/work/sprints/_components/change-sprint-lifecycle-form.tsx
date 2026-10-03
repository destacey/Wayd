'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { SprintDetailsDto } from '@/src/services/wayd-api'
import {
  useCompleteSprintMutation,
  useReopenSprintMutation,
  useStartSprintMutation,
} from '@/src/store/features/work-management/sprints-api'
import { isApiError } from '@/src/utils'
import { Alert, Modal, Space } from 'antd'

export enum SprintLifecycleAction {
  Start = 'Start',
  Complete = 'Complete',
  Reopen = 'Reopen',
}

const pastTense: Record<SprintLifecycleAction, string> = {
  [SprintLifecycleAction.Start]: 'started',
  [SprintLifecycleAction.Complete]: 'completed',
  [SprintLifecycleAction.Reopen]: 'reopened',
}

const presentParticiple: Record<SprintLifecycleAction, string> = {
  [SprintLifecycleAction.Start]: 'starting',
  [SprintLifecycleAction.Complete]: 'completing',
  [SprintLifecycleAction.Reopen]: 'reopening',
}

const explanation: Record<SprintLifecycleAction, string> = {
  [SprintLifecycleAction.Start]:
    'The sprint starts now. Its actual start replaces the default one.',
  [SprintLifecycleAction.Complete]:
    'The sprint ends now. Its actual end replaces the default one.',
  [SprintLifecycleAction.Reopen]:
    'The recorded completion is cleared, so the sprint ends on its default end again.',
}

export interface ChangeSprintLifecycleFormProps {
  sprint: SprintDetailsDto
  action: SprintLifecycleAction
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Confirms starting, completing or reopening a sprint.
 *
 * A team has one open sprint at a time. Starting while another is open
 * completes that one at the same instant, which the API refuses unless asked
 * for explicitly — confirming this dialog is that request.
 */
const ChangeSprintLifecycleForm = ({
  sprint,
  action,
  onFormComplete,
  onFormCancel,
}: ChangeSprintLifecycleFormProps) => {
  const messageApi = useMessage()

  const [startSprint] = useStartSprintMutation()
  const [completeSprint] = useCompleteSprintMutation()
  const [reopenSprint] = useReopenSprintMutation()

  const openSprint =
    action === SprintLifecycleAction.Start ? sprint.openSprint : undefined
  const errorMessage = `An unexpected error occurred while ${presentParticiple[action]} the sprint.`

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const request = { id: sprint.id, key: sprint.key }
        const response =
          action === SprintLifecycleAction.Start
            ? await startSprint({
                ...request,
                completeOpenSprint: !!openSprint,
                openSprint,
              })
            : action === SprintLifecycleAction.Complete
              ? await completeSprint(request)
              : await reopenSprint(request)

        if (response.error) throw response.error

        messageApi.success(`Successfully ${pastTense[action]} sprint.`)
        return true
      } catch (error) {
        messageApi.error(
          (isApiError(error) ? error.detail : undefined) ?? errorMessage,
        )
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage,
    permission: 'Permissions.Iterations.Update',
  })

  return (
    <Modal
      title={`Are you sure you want to ${action.toLowerCase()} this sprint?`}
      open={isOpen}
      onOk={handleOk}
      okText={`${action} Sprint`}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Space vertical>
        <div>
          {sprint.key} - {sprint.name}
        </div>
        <div>{explanation[action]}</div>
        {openSprint && (
          <Alert
            type="warning"
            showIcon
            title={`Starting this sprint also completes "${openSprint.name}" at the same moment.`}
            description="A team has one open sprint at a time."
          />
        )}
      </Space>
    </Modal>
  )
}

export default ChangeSprintLifecycleForm
