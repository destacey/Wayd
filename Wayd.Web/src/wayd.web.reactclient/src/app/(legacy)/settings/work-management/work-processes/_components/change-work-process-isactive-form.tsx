import { useMessage } from '@/src/components/contexts/messaging'
import { useChangeWorkProcessIsActiveMutation } from '@/src/store/features/work-management/work-process-api'
import { Modal, Typography } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'

const { Text } = Typography

export interface ChangeWorkProcessIsActiveFormProps {
  workProcessId: string
  workProcessName: string
  isActive: boolean
  onFormSave: () => void
  onFormCancel: () => void
}

const ChangeWorkProcessIsActiveForm = ({
  workProcessId,
  workProcessName,
  isActive,
  onFormSave,
  onFormCancel,
}: ChangeWorkProcessIsActiveFormProps) => {
  const messageApi = useMessage()

  const action = isActive ? 'Deactivate' : 'Activate'
  const actionLowerCase = action.toLowerCase()

  const [changeWorkProcessIsActive] = useChangeWorkProcessIsActiveMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const request = {
          id: workProcessId,
          isActive: !isActive,
        }
        const response = await changeWorkProcessIsActive(request)
        if (response.error) {
          throw response.error
        }
        messageApi.success(`Successfully ${actionLowerCase}d work process.`)
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          `Failed to ${actionLowerCase} work process. Error: ${apiError.detail}`,
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormSave,
    onCancel: onFormCancel,
    errorMessage: `An unexpected error occurred while ${actionLowerCase}ing the work process.`,
    permission: 'Permissions.WorkProcesses.Update',
  })

  if (isActive === undefined || isActive === null) return null

  return (
    <Modal
      title={`${action} Work Process`}
      open={isOpen}
      onOk={handleOk}
      okText={action}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Text>
        {`Are you sure you want to ${actionLowerCase} the work process ${workProcessName}?`}
      </Text>
    </Modal>
  )
}

export default ChangeWorkProcessIsActiveForm
