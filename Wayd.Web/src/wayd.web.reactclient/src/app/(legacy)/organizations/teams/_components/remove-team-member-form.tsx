'use client'

import { Modal } from 'antd'
import { useState } from 'react'
import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError, type ApiError } from '@/src/utils'
import {
  TeamMemberDto,
  useRemoveTeamMemberMutation,
  useRemoveTeamOfTeamsMemberMutation,
} from '@/src/store/features/organization/team-members-api'

interface Props {
  teamId: string
  teamType: 'Team' | 'TeamOfTeams'
  member: TeamMemberDto
  onFormComplete: () => void
  onFormCancel: () => void
}

const RemoveTeamMemberForm = ({ teamId, teamType, member, onFormComplete, onFormCancel }: Props) => {
  const messageApi = useMessage()
  const [removeTeamMember] = useRemoveTeamMemberMutation()
  const [removeTeamOfTeamsMember] = useRemoveTeamOfTeamsMemberMutation()
  const removeMember = teamType === 'Team' ? removeTeamMember : removeTeamOfTeamsMember
  const [isRemoving, setIsRemoving] = useState(false)

  const handleOk = async () => {
    setIsRemoving(true)
    try {
      const response = await removeMember({ teamId, employeeId: member.employee.id })
      if (response.error) throw response.error
      messageApi.success(`${member.employee.name} removed from team.`)
      onFormComplete()
    } catch (error) {
      const apiError: ApiError = isApiError(error) ? error : {}
      messageApi.error(apiError.detail ?? 'Failed to remove team member.')
    } finally {
      setIsRemoving(false)
    }
  }

  return (
    <Modal
      title="Remove Team Member"
      open={true}
      onOk={handleOk}
      okText="Remove"
      okType="danger"
      confirmLoading={isRemoving}
      onCancel={onFormCancel}
      keyboard={false}
      destroyOnHidden
    >
      <p>
        Remove <strong>{member.employee.name}</strong> from this team?
      </p>
    </Modal>
  )
}

export default RemoveTeamMemberForm
