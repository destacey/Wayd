'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { RoleDto } from '@/src/services/wayd-api'
import {
  useDeleteRoleMutation,
  useGetRoleUsersCountQuery,
} from '@/src/store/features/user-management/roles-api'
import { Alert, Flex, Modal } from 'antd'
import { useEffect } from 'react'
import { useConfirmModal } from '@/src/hooks'

export interface DeleteRoleFormProps {
  role: RoleDto
  onFormComplete: () => void
  onFormCancel: () => void
}

const DeleteRoleForm = ({
  role,
  onFormComplete,
  onFormCancel,
}: DeleteRoleFormProps) => {
  const messageApi = useMessage()

  const [deleteRoleMutation] = useDeleteRoleMutation()

  const editableRole = role && role.name !== 'Admin'

  const { data: countData } = useGetRoleUsersCountQuery(role.id, {
    skip: !role?.id,
  })

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const response = await deleteRoleMutation(role.id)

        if (response.error) {
          throw response.error
        }

        messageApi.success('Successfully deleted Role.')
        return true
      } catch (error: any) {
        if (error.status === 409 && error.detail) {
          messageApi.error(error.detail)
        } else {
          messageApi.error(error?.messages?.join() ?? 'Failed to delete role')
        }
        return false
      }
    },
    onComplete: onFormComplete,
    onCancel: onFormCancel,
    errorMessage: 'An unexpected error occurred while deleting the role.',
    permission: 'Permissions.Roles.Delete',
  })

  // Additional editableRole check
  useEffect(() => {
    if (!role) return
    if (!editableRole) {
      onFormCancel()
    }
  }, [role, editableRole, onFormCancel])

  return (
    <Modal
      title="Are you sure you want to delete this Role?"
      open={isOpen && editableRole}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      okButtonProps={{ disabled: countData !== undefined && countData > 0 }}
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Flex vertical gap="small">
        {role?.name}
        {countData && countData > 0 && (
          <Alert
            title={`This role is assigned to ${countData} user${countData !== 1 ? 's' : ''}. Roles assigned to users cannot be deleted. Please remove the role from all users before attempting to delete.`}
            type="error"
            showIcon
          />
        )}
      </Flex>
    </Modal>
  )
}

export default DeleteRoleForm
