'use client'

import { Form, Input, Modal, Select } from 'antd'
import { useState } from 'react'
import { toFormErrors } from '@/src/utils'
import { RoleListDto, UpdateRolePermissionsRequest } from '@/src/services/wayd-api'
import {
  useGetRoleQuery,
  useUpdatePermissionsMutation,
  useUpsertRoleMutation,
} from '@/src/store/features/user-management/roles-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'

const { Item } = Form
const { TextArea } = Input

export interface CreateRoleFormProps {
  roles: RoleListDto[]
  onFormCreate: (id: string) => void
  onFormCancel: () => void
}

interface CreateRoleFormValues {
  name: string
  description: string
}

const CreateRoleForm = ({
  roles,
  onFormCreate,
  onFormCancel,
}: CreateRoleFormProps) => {
  const [roleIdToCopyPermissions, setRoleIdToCopyPermissions] = useState<
    string | null
  >(null)
  const messageApi = useMessage()

  const { data: roleData } = useGetRoleQuery(roleIdToCopyPermissions!, {
    skip: !roleIdToCopyPermissions,
  })

  const [upsertRole] = useUpsertRoleMutation()
  const [updatePermissions] = useUpdatePermissionsMutation()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateRoleFormValues>({
      onSubmit: async (values: CreateRoleFormValues, form) => {
          try {
            const response = await upsertRole({
              name: values.name,
              description: values.description,
            })

            if (response.error) {
              throw response.error
            }

            if (roleIdToCopyPermissions && roleData?.permissions) {
              const request: UpdateRolePermissionsRequest = {
                roleId: response.data!,
                permissions: roleData.permissions!,
              }
              const updatePermissionsResponse = await updatePermissions(request)
              if (response.error) {
                console.error(
                  `Role created but an error was thrown while saving the permissions. Error: ${updatePermissionsResponse.error}`,
                )
              }
            }

            messageApi.success('Successfully created Role.')
            onFormCreate(response.data!)
            return false // Don't call onComplete — we already called onFormCreate
          } catch (error: any) {
            if (error.status === 422 && error.errors) {
              const formErrors = toFormErrors(error.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                'An unexpected error occurred while creating the role.',
              )
            }
            return false
          }
        },
      onComplete: () => {}, // handled in onSubmit via onFormCreate(id)
      onCancel: onFormCancel,
      errorMessage: 'An unexpected error occurred while creating the role.',
      permission: 'Permissions.Roles.Create',
    })

  return (
    <Modal
      title="Create Role"
      open={isOpen}
      onOk={handleOk}
      okButtonProps={{ disabled: !isValid }}
      okText="Create"
      confirmLoading={isSaving}
      onCancel={handleCancel}
      keyboard={false}
      destroyOnHidden
    >
      <Form form={form} size="small" layout="vertical" name="create-role-form">
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

        <Item name="isDefault" label="Copy Permissions From">
          {roles && (
            <Select
              allowClear
              showSearch
              placeholder="Select a Role"
              optionFilterProp="children"
              onChange={setRoleIdToCopyPermissions}
              filterOption={(input, option) =>
                (option?.label ?? '')
                  .toLowerCase()
                  .includes(input.toLowerCase())
              }
              options={
                roles?.map((role) => ({
                  value: role.id,
                  label: role.name,
                })) ?? []
              }
            />
          )}
        </Item>
      </Form>
    </Modal>
  )
}

export default CreateRoleForm
