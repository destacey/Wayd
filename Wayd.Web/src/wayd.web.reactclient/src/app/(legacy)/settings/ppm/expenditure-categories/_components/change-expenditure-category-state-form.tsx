'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { ExpenditureCategoryDetailsDto } from '@/src/services/wayd-api'
import {
  useActivateExpenditureCategoryMutation,
  useArchiveExpenditureCategoryMutation,
} from '@/src/store/features/ppm/expenditure-categories-api'
import { Modal, Space } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'

export enum ExpenditureCategoryStateAction {
  Activate = 'Activate',
  Archive = 'Archive',
}

export interface ChangeExpenditureCategoryStateFormProps {
  expenditureCategory: ExpenditureCategoryDetailsDto
  stateAction: ExpenditureCategoryStateAction
  onFormComplete: () => void
  onFormCancel: () => void
}

const ChangeExpenditureCategoryStateForm = ({
  expenditureCategory,
  stateAction,
  onFormComplete,
  onFormCancel,
}: ChangeExpenditureCategoryStateFormProps) => {
  const messageApi = useMessage()

  const [activateExpenditureCategoryMutation] =
    useActivateExpenditureCategoryMutation()
  const [archiveExpenditureCategoryMutation] =
    useArchiveExpenditureCategoryMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        let response = null
        if (stateAction === ExpenditureCategoryStateAction.Activate) {
          response = await activateExpenditureCategoryMutation(expenditureCategory.id)
        } else if (stateAction === ExpenditureCategoryStateAction.Archive) {
          response = await archiveExpenditureCategoryMutation(expenditureCategory.id)
        }

        if (response?.error) {
          throw response.error
        }

        messageApi.success(
          `Successfully ${stateAction}d expenditure category.`,
        )
        return true
      } catch (error) {
        const apiError: ApiError = isApiError(error) ? error : {}
        messageApi.error(
          apiError.detail ??
            `An unexpected error occurred while ${stateAction}ing the expenditure category.`,
        )
        console.log(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: `An unexpected error occurred while ${stateAction}ing the expenditure category.`,
    permission: 'Permissions.ExpenditureCategories.Update',
  })

  return (
    <Modal
      title={`Are you sure you want to ${stateAction} this Expenditure Category?`}
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
          {expenditureCategory?.id} - {expenditureCategory?.name}
        </div>
        {'This action cannot be undone.'}
      </Space>
    </Modal>
  )
}

export default ChangeExpenditureCategoryStateForm
