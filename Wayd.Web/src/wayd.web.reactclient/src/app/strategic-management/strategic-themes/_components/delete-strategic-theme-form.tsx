'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { StrategicThemeDetailsDto } from '@/src/services/wayd-api'
import { useDeleteStrategicThemeMutation } from '@/src/store/features/strategic-management/strategic-themes-api'
import { Modal } from 'antd'
import { isApiError, type ApiError } from '@/src/utils'

export interface DeleteStrategicThemeFormProps {
  strategicTheme: StrategicThemeDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

const DeleteStrategicThemeForm = ({
  strategicTheme,
  onFormComplete,
  onFormCancel,
}: DeleteStrategicThemeFormProps) => {
  const messageApi = useMessage()

  const [deleteStrategicThemeMutation] = useDeleteStrategicThemeMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteStrategicThemeMutation({
          strategicThemeId: strategicTheme.id,
          cacheKey: strategicTheme.key,
        })
        if (response.error) {
          throw response.error
        }
        messageApi.success('Successfully deleted Strategic Theme.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An unexpected error occurred while deleting the strategic theme.',
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An unexpected error occurred while deleting the strategic theme.',
    permission: 'Permissions.StrategicThemes.Delete',
  })

  return (
    <Modal
      title="Are you sure you want to delete this Strategic Theme?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {strategicTheme?.key} - {strategicTheme?.name}
    </Modal>
  )
}

export default DeleteStrategicThemeForm
