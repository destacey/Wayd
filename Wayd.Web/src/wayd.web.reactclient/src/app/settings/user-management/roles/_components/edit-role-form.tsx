'use client'

import { Form, Input, Modal } from 'antd'
import { useEffect } from 'react'
import { toFormErrors } from '@/src/utils'
import { CreateOrUpdateRoleRequest, RoleDto } from '@/src/services/wayd-api'
import { useUpsertRoleMutation } from '@/src/store/features/user-management/roles-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input

export interface EditRoleFormProps {
  role: RoleDto
  onFormComplete: () => void
  onFormCancel: () => void
}

const EditRoleForm = ({
  role,
  onFormComplete,
  onFormCancel,
}: EditRoleFormProps) => {
  const messageApi = useMessage()

  // The hook's permission check handles the basic permission claim.
  // We also need to check that the role is editable (not the Admin system role).
  const editableRole = role && role.name !== 'Admin'

  const [upsertRole] = useUpsertRoleMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateOrUpdateRoleRequest>({
      onSubmit: async (values: CreateOrUpdateRoleRequest, form) => {
          try {
            const response = await upsertRole({
              id: role.id,
              name: values.name,
              description: values.description,
            })

            if (response.error) {
              throw response.error
            }

            messageApi.success('Successfully updated Role.')
            return true
          } catch (error: any) {
            if (error.status === 422 && error.errors) {
              const formErrors = toFormErrors(error.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else if (error.statusCode === 409 && error.exception) {
              messageApi.error(error.exception)
            } else {
              messageApi.error(
                'An unexpected error occurred while updating the role.',
              )
            }
            return false
          }
        },
      onComplete: onFormComplete,
      onCancel: onFormCancel,
      errorMessage: 'An unexpected error occurred while updating the role.',
      permission: 'Permissions.Roles.Update',
    })

  // Additional editableRole check — if the role is not editable, cancel
  useEffect(() => {
    if (!role) return
    if (!editableRole) {
      messageApi.error('You do not have permission to edit role.')
      onFormCancel()
    }
  }, [role, editableRole, messageApi, onFormCancel])

  useEffect(() => {
    if (!role) return
    form.setFieldsValue({
      id: role.id,
      name: role.name,
      description: role.description,
    })
  }, [role, form])

  return (
    <Modal
      title="Edit Role"
      open={isOpen && editableRole}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Save"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form form={form} size="small" layout="vertical" name="edit-role-form">
        <Item label="Name" name="name" rules={[{ required: true }]}>
          <TextArea
            autoSize={{ minRows: 1, maxRows: 4 }}
            showCount
            maxLength={256}
          />
        </Item>

        <Item name="description" label="Description">
          <TextArea
            autoSize={{ minRows: 6, maxRows: 10 }}
            showCount
            maxLength={1024}
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditRoleForm
