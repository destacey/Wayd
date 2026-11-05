'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'
import { ProductTeamLinkDto } from '@/src/services/wayd-api'
import { useUnlinkProductTeamMutation } from '@/src/store/features/product-management/products-api'
import { toApiError } from '@/src/utils'
import { Modal, Typography } from 'antd'

const { Text } = Typography

export interface UnlinkProductTeamFormProps {
  link: ProductTeamLinkDto
  onFormComplete: () => void
  onFormCancel: () => void
}

/**
 * Confirms unlinking a team from the product the link is on — on a rolled-up row, a product beneath the page's.
 */
const UnlinkProductTeamForm = ({
  link,
  onFormComplete,
  onFormCancel,
}: UnlinkProductTeamFormProps) => {
  const messageApi = useMessage()
  const [unlinkProductTeam] = useUnlinkProductTeamMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await unlinkProductTeam({
          productId: link.product.id,
          teamId: link.team.id,
        })
        if (response.error) throw response.error

        messageApi.success('Team unlinked.')
        return true
      } catch (error) {
        messageApi.error(
          toApiError(error).detail ??
            'An error occurred while unlinking the team. Please try again.',
        )
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage:
      'An error occurred while unlinking the team. Please try again.',
    permission: 'Permissions.Products.Update',
  })

  return (
    <Modal
      title="Unlink Team"
      open={isOpen}
      onOk={handleOk}
      okText="Unlink"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Text>
        Unlink <Text strong>{link.team.name}</Text> from{' '}
        <Text strong>{link.product.name}</Text>?
      </Text>
    </Modal>
  )
}

export default UnlinkProductTeamForm
