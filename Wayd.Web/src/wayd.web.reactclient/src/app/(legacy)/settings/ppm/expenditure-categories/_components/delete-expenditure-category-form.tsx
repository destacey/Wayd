'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { ExpenditureCategoryDetailsDto } from '@/src/services/wayd-api'
import { useDeleteExpenditureCategoryMutation } from '@/src/store/features/ppm/expenditure-categories-api'
import { Modal } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'

export interface DeleteExpenditureCategoryFormProps {
  expenditureCategory: ExpenditureCategoryDetailsDto
  onFormComplete: () => void
  onFormCancel: () => void
}

const DeleteExpenditureCategoryForm = ({
  expenditureCategory,
  onFormComplete,
  onFormCancel,
}: DeleteExpenditureCategoryFormProps) => {
  const messageApi = useMessage()

  const [deleteExpenditureCategory] =
    useDeleteExpenditureCategoryMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteExpenditureCategory(expenditureCategory.id)
        if (response.error) {
          throw response.error
        }
        messageApi.success('Successfully deleted expenditure category.')
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            'An unexpected error occurred while deleting the expenditure category.',
        )
        console.log(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An unexpected error occurred while deleting the expenditure category.',
    permission: 'Permissions.ExpenditureCategories.Delete',
  })

  return (
    <Modal
      title="Are you sure you want to delete this Expenditure Category?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {expenditureCategory?.id} - {expenditureCategory?.name}
    </Modal>
  )
}

export default DeleteExpenditureCategoryForm
