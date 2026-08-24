'use client'

import { Form, Modal, Select } from 'antd'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'
import { isApiError, type ApiError } from '@/src/utils'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'
import { useGetTeamMemberRolesQuery } from '@/src/store/features/organization/team-member-roles-api'
import {
  useAddTeamMemberMutation,
  useAddTeamOfTeamsMemberMutation,
  useGetTeamMembersQuery,
  useGetTeamOfTeamsMembersQuery,
} from '@/src/store/features/organization/team-members-api'

const { Item: FormItem } = Form

interface Props {
  teamId: string
  teamType: 'Team' | 'TeamOfTeams'
  onFormComplete: () => void
  onFormCancel: () => void
}

interface FormValues {
  employeeId: string
  roleIds: string[]
}

const AddTeamMemberForm = ({ teamId, teamType, onFormComplete, onFormCancel }: Props) => {
  const messageApi = useMessage()
  const { data: employeeOptions } = useGetEmployeeOptionsQuery(false)
  const { data: roleOptions } = useGetTeamMemberRolesQuery(false)
  const { data: teamMembers } = useGetTeamMembersQuery({ teamId }, { skip: teamType !== 'Team' })
  const { data: totMembers } = useGetTeamOfTeamsMembersQuery({ teamId }, { skip: teamType !== 'TeamOfTeams' })
  const currentMembers = teamType === 'Team' ? teamMembers : totMembers

  const [addTeamMember] = useAddTeamMemberMutation()
  const [addTeamOfTeamsMember] = useAddTeamOfTeamsMemberMutation()
  const addMember = teamType === 'Team' ? addTeamMember : addTeamOfTeamsMember

  const currentMemberIds = new Set(currentMembers?.map((m) => m.employee.id) ?? [])
  const filteredEmployeeOptions = employeeOptions?.filter((e) => !currentMemberIds.has(e.value as string)) ?? []

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<FormValues>({
      onSubmit: async (values) => {
        try {
          const response = await addMember({
            teamId,
            request: { employeeId: values.employeeId, roleIds: values.roleIds },
          })
          if (response.error) throw response.error
          messageApi.success('Team member added.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          messageApi.error(apiError.detail ?? 'Failed to add team member.')
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'Failed to add team member.',
      permission: 'Permissions.Teams.Update',
    })

  const roleSelectOptions = roleOptions?.map((r) => ({ label: r.name, value: r.id })) ?? []

  return (
    <Modal
      title="Add Team Member"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Add"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form form={form} size="small" layout="vertical" name="add-team-member-form">
        <FormItem name="employeeId" label="Employee" rules={[{ required: true }]}>
          <Select
            showSearch
            placeholder="Select an employee"
            optionFilterProp="label"
            options={filteredEmployeeOptions}
          />
        </FormItem>
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

export default AddTeamMemberForm
