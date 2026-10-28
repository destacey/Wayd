'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { IntakeRequestTagOptionDto } from '@/src/services/wayd-api'
import { useSetIntakeRequestTagActiveMutation } from '@/src/store/features/demand-management/demand-management-api'
import { Modal } from 'antd'
import { useConfirmModal } from '@/src/hooks'
import { toApiError } from '@/src/utils'

export interface ChangeTagActiveFormProps {
  axisId: string
  tag: IntakeRequestTagOptionDto
  /** True puts the tag back into use, false retires it. */
  isActive: boolean
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Retires a tag from new use, or puts it back.
 *
 * The request count is spelled out when retiring, because that is the number
 * the decision turns on: those requests keep the tag, and the confirmation says
 * so rather than leaving the reader to assume it strips them.
 */
const ChangeTagActiveForm = ({
  axisId,
  tag,
  isActive,
  onFormComplete,
  onFormCancel,
}: ChangeTagActiveFormProps) => {
  const messageApi = useMessage()

  const [setTagActive] = useSetIntakeRequestTagActiveMutation()

  const gerund = isActive ? 'activating' : 'deactivating'

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        await setTagActive({
          categoryId: axisId,
          tagId: tag.id,
          isActive,
        }).unwrap()
        messageApi.success(isActive ? 'Tag activated.' : 'Tag deactivated.')
        return true
      } catch (error) {
        messageApi.error(
          toApiError(error).detail ??
            `An unexpected error occurred while ${gerund} the tag.`,
        )
        console.error(error)
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: `An unexpected error occurred while ${gerund} the tag.`,
    permission: 'Permissions.IntakeRequests.Administer',
  })

  return (
    <Modal
      title={`${isActive ? 'Activate' : 'Deactivate'} this tag?`}
      open={isOpen}
      onOk={handleOk}
      okText={isActive ? 'Activate' : 'Deactivate'}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      {tag.name}
      {!isActive && (
        <p>
          {tag.requestCount > 0
            ? `${tag.requestCount} request(s) carry this tag and keep it. It can no longer be given to a request.`
            : 'The tag can no longer be given to a request.'}
        </p>
      )}
    </Modal>
  )
}

export default ChangeTagActiveForm
