'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { ProjectPortfolioDetailsDto } from '@/src/services/wayd-api'
import {
  useActivatePortfolioMutation,
  useArchivePortfolioMutation,
  useClosePortfolioMutation,
} from '@/src/store/features/ppm/portfolios-api'
import { Alert, Modal, Space } from 'antd'
import { isApiError, type ApiError } from '@/src/utils'

export enum PortfolioStatusAction {
  Activate = 'Activate',
  Close = 'Close',
  Archive = 'Archive',
}

const statusActionToPastTense = (statusAction: PortfolioStatusAction) => {
  switch (statusAction) {
    case PortfolioStatusAction.Activate:
      return 'activated'
    case PortfolioStatusAction.Close:
      return 'closed'
    case PortfolioStatusAction.Archive:
      return 'archived'
    default:
      return statusAction
  }
}

const statusActionToPresentTense = (statusAction: PortfolioStatusAction) => {
  switch (statusAction) {
    case PortfolioStatusAction.Activate:
      return 'activating'
    case PortfolioStatusAction.Close:
      return 'closing'
    case PortfolioStatusAction.Archive:
      return 'archiving'
    default:
      return statusAction
  }
}

export interface ChangePortfolioStatusFormProps {
  portfolio: ProjectPortfolioDetailsDto
  statusAction: PortfolioStatusAction
  onFormComplete: () => void
  onFormCancel: () => void
}

const ChangePortfolioStatusForm = ({
  portfolio,
  statusAction,
  onFormComplete,
  onFormCancel,
}: ChangePortfolioStatusFormProps) => {
  const messageApi = useMessage()

  const [activatePortfolioMutation] = useActivatePortfolioMutation()
  const [closePortfolioMutation] = useClosePortfolioMutation()
  const [archivePortfolioMutation] = useArchivePortfolioMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const request = { id: portfolio.id, cacheKey: portfolio.key }
        let response = null
        if (statusAction === PortfolioStatusAction.Activate) {
          response = await activatePortfolioMutation(request)
        } else if (statusAction === PortfolioStatusAction.Close) {
          response = await closePortfolioMutation(request)
        } else if (statusAction === PortfolioStatusAction.Archive) {
          response = await archivePortfolioMutation(request)
        }

        if (response?.error) throw response.error

        messageApi.success(
          `Successfully ${statusActionToPastTense(statusAction)} Portfolio.`,
        )
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            `An unexpected error occurred while ${statusActionToPresentTense(statusAction)} the portfolio.`,
        )
        console.log(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: `An unexpected error occurred while ${statusActionToPresentTense(statusAction)} the portfolio.`,
    permission: 'Permissions.ProjectPortfolios.Update',
  })

  return (
    <Modal
      title={`Are you sure you want to ${statusAction} this Portfolio?`}
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
          {portfolio?.key} - {portfolio?.name}
        </div>
        <Alert title="This action cannot be undone." type="warning" showIcon />
      </Space>
    </Modal>
  )
}

export default ChangePortfolioStatusForm
