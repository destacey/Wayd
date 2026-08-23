import { TeamMembershipDto } from '@/src/services/wayd-api'
import { TeamTypeName } from '../types'
import { Descriptions, Modal } from 'antd'
import dayjs from 'dayjs'
import { useDeleteTeamMembershipMutation } from '@/src/store/features/organizations/team-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useConfirmModal } from '@/src/hooks'

const { Item } = Descriptions

interface DeleteTeamMembershipFormProps {
  membership: TeamMembershipDto
  teamType: TeamTypeName
  onFormSave: () => void
  onFormCancel: () => void
}

const mapToRequestValues = (
  membership: TeamMembershipDto,
  teamType: TeamTypeName,
) => {
  return {
    teamMembershipId: membership.id,
    teamId: membership.child.id,
    parentTeamId: membership.parent.id,
    teamType,
  }
}

const DeleteTeamMembershipForm = ({
  membership,
  teamType,
  onFormSave,
  onFormCancel,
}: DeleteTeamMembershipFormProps) => {
  const messageApi = useMessage()

  const [deleteTeamMembershipMutation] = useDeleteTeamMembershipMutation()

  const { isOpen, isSaving, handleOk, handleCancel } = useConfirmModal({
    onSubmit: async () => {
      try {
        const request = mapToRequestValues(membership, teamType)
        await deleteTeamMembershipMutation(request).unwrap()
        messageApi.success('Successfully deleted team membership.')
        return true
      } catch (error) {
        messageApi.error(
          'An unexpected error occurred while deleting the team membership.',
        )
        console.log(error)
        return false
      }
    },
    onComplete: onFormSave,
    onCancel: onFormCancel,
    errorMessage:
      'An unexpected error occurred while deleting the team membership.',
    permission: 'Permissions.Teams.ManageTeamMemberships',
  })

  return (
    <Modal
      title="Are you sure you want to delete this team membership?"
      open={isOpen}
      onOk={handleOk}
      okText="Delete"
      okType="danger"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false} // disable esc key to close modal
      destroyOnHidden
    >
      <Descriptions size="small" column={1}>
        <Item label="Team">{membership?.child.name}</Item>
        <Item label="Parent Team">{membership?.parent.name}</Item>
        <Item label="Start">
          {dayjs(membership?.start).format('M/D/YYYY')}
        </Item>
        <Item label="End">
          {membership?.end
            ? dayjs(membership?.end).format('M/D/YYYY')
            : null}
        </Item>
      </Descriptions>
    </Modal>
  )
}

export default DeleteTeamMembershipForm
