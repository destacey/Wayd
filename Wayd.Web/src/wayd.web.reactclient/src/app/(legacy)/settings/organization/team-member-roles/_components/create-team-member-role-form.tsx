'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import { useCreateTeamMemberRoleMutation } from '@/src/store/features/organization/team-member-roles-api'
import { toFormErrors, isApiError, type ApiError } from '@/src/utils'
import { Form, Input, Modal } from 'antd'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input

export interface CreateTeamMemberRoleFormProps {
  onFormComplete: () => void
  onFormCancel: () => void
}

interface FormValues {
  name: string
  description?: string
}

const CreateTeamMemberRoleForm = ({
  onFormComplete,
  onFormCancel,
}: CreateTeamMemberRoleFormProps) => {
  const messageApi = useMessage()
  const [createTeamMemberRole] = useCreateTeamMemberRoleMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<FormValues>({
      onSubmit: async (values, form) => {
        try {
          const response = await createTeamMemberRole({
            name: values.name,
            description: values.description || undefined,
          })
          if (response.error) throw response.error
          messageApi.success('Team member role created successfully.')
          return true
        } catch (error) {
          const apiError: ApiError = isApiError(error) ? error : {}
          if (apiError.status === 422 && apiError.errors) {
            form.setFields(toFormErrors(apiError.errors))
            messageApi.error('Correct the validation error(s) to continue.')
          } else {
            messageApi.error(
              apiError.detail ??
                'An error occurred while creating the team member role.',
            )
          }
          return false
        }
      },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'An error occurred while creating the team member role.',
      permission: 'Permissions.TeamMemberRoles.Create',
    })

  return (
    <Modal
      title="Create Team Member Role"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form
        form={form}
        size="small"
        layout="vertical"
        name="create-team-member-role-form"
      >
        <Item
          label="Name"
          name="name"
          rules={[
            { required: true, message: 'Name is required' },
            { max: 128 },
          ]}
        >
          <Input showCount maxLength={128} />
        </Item>
        <Item label="Description" name="description" rules={[{ max: 1024 }]}>
          <TextArea showCount maxLength={1024} rows={4} />
        </Item>
      </Form>
    </Modal>
  )
}

export default CreateTeamMemberRoleForm
