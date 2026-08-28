'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { TeamMemberRoleDto } from '@/src/services/wayd-api'
import { useDeleteTeamMemberRoleMutation } from '@/src/store/features/organization/team-member-roles-api'
import { isApiError, type ApiError } from '@/src/utils'
import { Alert, Modal } from 'antd'
import { useState } from 'react'

export interface DeleteTeamMemberRoleFormProps {
  role: TeamMemberRoleDto
  onFormComplete: () => void
  onFormCancel: () => void
}

const DeleteTeamMemberRoleForm = ({
  role,
  onFormComplete,
  onFormCancel,
}: DeleteTeamMemberRoleFormProps) => {
  const messageApi = useMessage()
  const [deleteTeamMemberRole] = useDeleteTeamMemberRoleMutation()
  const [isDeleting, setIsDeleting] = useState(false)

  const handleOk = async () => {
    setIsDeleting(true)
    try {
      const response = await deleteTeamMemberRole(role.id)
      if (response.error) throw response.error
      messageApi.success(`Team member role "${role.name}" deleted.`)
      onFormComplete()
    } catch (error) {
      const apiError: ApiError = isApiError(error) ? error : {}
      messageApi.error(
        apiError.detail ?? 'An error occurred while deleting the team member role.',
      )
    } finally {
      setIsDeleting(false)
    }
  }

  return (
    <Modal
      title="Delete Team Member Role"
      open={true}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isDeleting}
      onCancel={onFormCancel}
      keyboard={false}
      destroyOnHidden
    >
      <p>
        Are you sure you want to delete <strong>{role.name}</strong>?
      </p>
      <Alert
        type="warning"
        showIcon
        message="This action cannot be undone. Roles currently assigned to team members cannot be deleted."
        style={{ marginTop: 12 }}
      />
    </Modal>
  )
}

export default DeleteTeamMemberRoleForm
