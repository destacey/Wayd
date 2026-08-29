'use client'

import { Form, Modal, Select } from 'antd'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'
import { useGetTeamMemberRolesQuery } from '@/src/store/features/organization/team-member-roles-api'
import {
  TeamMemberDto,
  useUpdateTeamMemberMutation,
  useUpdateTeamOfTeamsMemberMutation,
} from '@/src/store/features/organization/team-members-api'

const { Item: FormItem } = Form

interface Props {
  teamId: string
  teamType: 'Team' | 'TeamOfTeams'
  member: TeamMemberDto
  onFormComplete: () => void
  onFormCancel: () => void
}

interface FormValues {
  roleIds: string[]
}

const EditTeamMemberForm = ({ teamId, teamType, member, onFormComplete, onFormCancel }: Props) => {
  const messageApi = useMessage()
  const { data: roleOptions } = useGetTeamMemberRolesQuery(false)
  const [updateTeamMember] = useUpdateTeamMemberMutation()
  const [updateTeamOfTeamsMember] = useUpdateTeamOfTeamsMemberMutation()
  const updateMember = teamType === 'Team' ? updateTeamMember : updateTeamOfTeamsMember

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<FormValues>({
      onSubmit: async (values) => {
        try {
          const response = await updateMember({
            teamId,
            employeeId: member.employee.id,
            request: { roleIds: values.roleIds },
          })
          if (response.error) throw response.error
          messageApi.success('Team member updated.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          messageApi.error(apiError.detail ?? 'Failed to update team member.')
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to update team member.',
      permission: 'Permissions.Teams.Update',
    })

  const roleSelectOptions = roleOptions?.map((r) => ({ label: r.name, value: r.id })) ?? []

  return (
    <Modal
      title={`Edit ${member.employee.name}`}
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="edit-team-member-form"
        initialValues={{ roleIds: member.roles.map((r) => r.id) }}
      >
        <FormItem name="roleIds" label="Role(s)" rules={[{ required: true }]}>
          <Select
            mode="multiple"
            showSearch
            placeholder="Select one or more roles"
            optionFilterProp="label"
            options={roleSelectOptions}
          />
        </FormItem>
      </Form>
    </Modal>
  )
}

export default EditTeamMemberForm
