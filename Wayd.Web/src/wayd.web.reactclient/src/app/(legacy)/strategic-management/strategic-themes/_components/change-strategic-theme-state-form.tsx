'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { StrategicThemeDetailsDto } from '@/src/services/wayd-api'
import {
  useActivateStrategicThemeMutation,
  useArchiveStrategicThemeMutation,
} from '@/src/store/features/strategic-management/strategic-themes-api'
import { Modal, Space } from 'antd'
import { isApiError, type ApiError } from '@/src/utils'

export enum StrategicThemeStateAction {
  Activate = 'Activate',
  Archive = 'Archive',
}

export interface ChangeStrategicThemeStateFormProps {
  strategicTheme: StrategicThemeDetailsDto
  stateAction: StrategicThemeStateAction
  onFormComplete: () => void
  onFormCancel: () => void
}

const ChangeStrategicThemeStateForm = ({
  strategicTheme,
  stateAction,
  onFormComplete,
  onFormCancel,
}: ChangeStrategicThemeStateFormProps) => {
  const messageApi = useMessage()

  const [activateStrategicThemeMutation] = useActivateStrategicThemeMutation()
  const [archiveStrategicThemeMutation] = useArchiveStrategicThemeMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        let response = null
        if (stateAction === StrategicThemeStateAction.Activate) {
          response = await activateStrategicThemeMutation({
            id: strategicTheme.id,
            cacheKey: strategicTheme.key,
          })
        } else if (stateAction === StrategicThemeStateAction.Archive) {
          response = await archiveStrategicThemeMutation({
            id: strategicTheme.id,
            cacheKey: strategicTheme.key,
          })
        }

        if (response?.error) {
          throw response.error
        }

        messageApi.success(
          `Successfully ${stateAction}d Strategic Theme.`,
        )
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            `An unexpected error occurred while ${stateAction}ing the strategic theme.`,
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: `An unexpected error occurred while ${stateAction}ing the strategic theme.`,
    permission: 'Permissions.StrategicThemes.Update',
  })

  return (
    <Modal
      title={`Are you sure you want to ${stateAction} this Strategic Theme?`}
      open={isOpen}
      onOk={handleOk}
      okText={stateAction}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Space vertical>
        <div>
          {strategicTheme?.key} - {strategicTheme?.name}
        </div>
        {'This action cannot be undone.'}
      </Space>
    </Modal>
  )
}

export default ChangeStrategicThemeStateForm
