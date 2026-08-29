'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { DeleteOidcProviderResult, OidcProviderListItemDto } from '@/src/services/wayd-api'
import { useDeleteOidcProviderMutation } from '@/src/store/features/user-management/oidc-providers-api'
import { Alert, Flex, Modal, Typography } from 'antd'
import { useState } from 'react'
import { useConfirmModal } from '@/src/hooks'

const { Text } = Typography

export interface DeleteOidcProviderFormProps {
  provider: OidcProviderListItemDto
  onFormComplete: () => void
  onFormCancel: () => void
}

const isDeleteConflict = (error: unknown): error is DeleteOidcProviderResult =>
  typeof error === 'object' &&
  error !== null &&
  'activeIdentityCount' in error &&
  'deleted' in error

const DeleteOidcProviderForm = ({
  provider,
  onFormComplete,
  onFormCancel,
}: DeleteOidcProviderFormProps) => {
  const messageApi = useMessage()
  const [deleteOidcProvider] = useDeleteOidcProviderMutation()
  const [conflictCount, setConflictCount] = useState<number | null>(null)

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteOidcProvider(provider.id)
        if (response.error) throw response.error
        messageApi.success('Identity provider deleted successfully.')
        return true
      } catch (error) {
        if (isDeleteConflict(error)) {
          setConflictCount(error.activeIdentityCount)
          return false
        }
        messageApi.error(
          'An unexpected error occurred while deleting the identity provider.',
        )
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: () => {
      setConflictCount(null)
      onFormCancel()
    },
    permission: 'Permissions.OidcProviders.Delete',
  })

  if (conflictCount !== null) {
    return (
      <Modal
        title="Cannot Delete Identity Provider"
        open={isOpen}
        onOk={() => {
          setConflictCount(null)
          onFormCancel()
        }}
        okText="OK"
        cancelButtonProps={{ style: { display: 'none' } }}
        onCancel={() => {
          setConflictCount(null)
          onFormCancel()
        }}
        keyboard={false}
        destroyOnHidden
      >
        <Text>
          <strong>{conflictCount}</strong>{' '}
          {conflictCount === 1 ? 'user has' : 'users have'} an active identity
          bound to <strong>{provider.displayName}</strong>. Rebind or unlink
          {conflictCount === 1 ? ' them' : ' all affected users'} first, then
          try again.
        </Text>
      </Modal>
    )
  }

  return (
    <Modal
      title="Are you sure you want to delete this identity provider?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Flex vertical gap={12}>
        <Text>
          <strong>{provider.displayName}</strong> ({provider.name})
        </Text>
        <Alert
          type="warning"
          showIcon
          title="This cannot be completed if any users have an active identity bound to this provider. Rebind those users first."
        />
      </Flex>
    </Modal>
  )
}

export default DeleteOidcProviderForm
