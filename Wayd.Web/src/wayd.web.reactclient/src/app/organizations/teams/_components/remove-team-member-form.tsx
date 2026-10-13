'use client'

import { Checkbox, Modal } from 'antd'
import { useState } from 'react'
import { useMessage } from '@/src/components/contexts/messaging'
import { useFeatureFlag } from '@/src/hooks'
import { isApiError, todayCalendarDate, type ApiError } from '@/src/utils'
import {
  TeamMemberDto,
  useRemoveTeamMemberMutation,
  useRemoveTeamOfTeamsMemberMutation,
} from '@/src/store/features/organization/team-members-api'
import {
  useChangeEmployeeAllocationsMutation,
  useGetEmployeeAllocationsQuery,
} from '@/src/store/features/organization/employee-allocations-api'

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

  // Membership and allocation are separate, so removing a member leaves their
  // time on the team in place unless it is ended here too.
  const { isEnabled: capacityEnabled } = useFeatureFlag(
    'team-capacity-management',
  )
  const { data: allocations } = useGetEmployeeAllocationsQuery(
    { employeeId: member.employee.id },
    { skip: !capacityEnabled },
  )
  const [changeAllocations] = useChangeEmployeeAllocationsMutation()
  const [endAllocation, setEndAllocation] = useState(true)

  const today = todayCalendarDate()
  const openAllocations = (allocations ?? []).filter(
    (a) => a.team.id === teamId && a.canManage && (!a.end || a.end >= today),
  )
  const canEndAllocation = capacityEnabled && openAllocations.length > 0

  const handleOk = async () => {
    setIsRemoving(true)
    try {
      // Before the removal: changing a team's allocations needs membership of
      // it, which someone removing themselves no longer has afterwards.
      if (canEndAllocation && endAllocation) {
        const roleIds = [...new Set(openAllocations.map((a) => a.role?.id))]
        const ended = await changeAllocations({
          employeeId: member.employee.id,
          request: {
            changes: roleIds.map((roleId) => ({
              teamId,
              roleId,
              start: today,
              percent: 0,
            })),
          },
        })
        if (ended.error) throw ended.error
      }

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
      {canEndAllocation && (
        <Checkbox
          checked={endAllocation}
          onChange={(e) => setEndAllocation(e.target.checked)}
        >
          Also end their allocation to this team from today
        </Checkbox>
      )}
    </Modal>
  )
}

export default RemoveTeamMemberForm
