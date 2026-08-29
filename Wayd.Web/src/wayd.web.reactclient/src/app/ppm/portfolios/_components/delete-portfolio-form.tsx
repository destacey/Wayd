'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { ProjectPortfolioDetailsDto } from '@/src/services/wayd-api'
import { useDeletePortfolioMutation } from '@/src/store/features/ppm/portfolios-api'
import { Modal } from 'antd'
import { isApiError, type ApiError } from '@/src/utils'

export interface DeletePortfolioFormProps {
  portfolio: ProjectPortfolioDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

const DeletePortfolioForm = ({
  portfolio,
  onFormComplete,
  onFormCancel,
}: DeletePortfolioFormProps) => {
  const messageApi = useMessage()

  const [deletePortfolioMutation] = useDeletePortfolioMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deletePortfolioMutation({
          portfolioId: portfolio.id,
          cacheKey: portfolio.key,
        })
        if (response.error) throw response.error

        // TODO: not working because the parent page is gone
        messageApi.success('Successfully deleted Portfolio.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An unexpected error occurred while deleting the portfolio.',
        )
        console.log(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An unexpected error occurred while deleting the portfolio.',
    permission: 'Permissions.ProjectPortfolios.Delete',
  })

  return (
    <Modal
      title="Are you sure you want to delete this Portfolio?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      {portfolio?.key} - {portfolio?.name}
    </Modal>
  )
}

export default DeletePortfolioForm
