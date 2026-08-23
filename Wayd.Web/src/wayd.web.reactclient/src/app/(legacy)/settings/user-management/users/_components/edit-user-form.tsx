'use client'

import { Form, Input, Modal, Select } from 'antd'
import { toFormErrors } from '@/src/utils'
import {
  useGetUsersQuery,
  useUpdateUserMutation,
} from '@/src/store/features/user-management/users-api'
import { useGetEmployeesQuery } from '@/src/store/features/organizations/employee-api'
import { UserDetailsDto } from '@/src/services/wayd-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

export interface EditUserFormProps {
  user: UserDetailsDto
  onFormUpdate: () => void
  onFormCancel: () => void
}

interface EditUserFormValues {
  firstName: string
  lastName: string
  email: string
  phoneNumber?: string
  employeeId?: string
}

const EditUserForm = ({
  user,
  onFormUpdate,
  onFormCancel,
}: EditUserFormProps) => {
  const messageApi = useMessage()
  const [updateUser] = useUpdateUserMutation()
  const { data: employeesData, isLoading: employeesLoading } =
    useGetEmployeesQuery(false)
  const { data: usersData } = useGetUsersQuery()

  const employeeOptions = (() => {
    if (!employeesData) return []
    const linkedEmployeeIds = new Set(
      usersData
        ?.filter((u) => u.employee?.id && u.id !== user.id)
        .map((u) => u.employee!.id) ?? [],
    )
    return employeesData
      .filter((e) => e.isActive && !linkedEmployeeIds.has(e.id))
      .map((e) => ({ value: e.id, label: e.displayName }))
  })()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<EditUserFormValues>({
      onSubmit: async (values: EditUserFormValues, form) => {
          try {
            const response = await updateUser({
              id: user.id,
              firstName: values.firstName,
              lastName: values.lastName,
              email: values.email,
              phoneNumber: values.phoneNumber || undefined,
              employeeId: values.employeeId || undefined,
            })

            if (response.error) {
              throw response.error
            }

            messageApi.success('Successfully updated user.')
            onFormUpdate()
            return false
          } catch (error: any) {
            if (error.status === 422 && error.errors) {
              const formErrors = toFormErrors(error.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                error.detail ??
                  'An unexpected error occurred while updating the user.',
              )
            }
            return false
          }
        },
      onComplete: () => {},
      onCancel: onFormCancel,
      errorMessage: 'An unexpected error occurred while updating the user.',
      permission: 'Permissions.Users.Update',
    })

  return (
    <Modal
      title="Edit User"
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
        name="edit-user-form"
        initialValues={{
          firstName: user.firstName,
          lastName: user.lastName,
          email: user.email,
          phoneNumber: user.phoneNumber,
          employeeId: user.employee?.id,
        }}
      >
        <Item
          label="Email / Username"
          name="email"
          rules={[
            { required: true, message: 'Email is required' },
            { type: 'email', message: 'Please enter a valid email' },
          ]}
        >
          <Input maxLength={256} />
        </Item>

        <Item
          label="First Name"
          name="firstName"
          rules={[{ required: true, message: 'First name is required' }]}
        >
          <Input maxLength={64} />
        </Item>

        <Item
          label="Last Name"
          name="lastName"
          rules={[{ required: true, message: 'Last name is required' }]}
        >
          <Input maxLength={64} />
        </Item>

        <Item label="Phone Number" name="phoneNumber">
          <Input maxLength={20} />
        </Item>

        <Item label="Employee" name="employeeId">
          <Select
            allowClear
            showSearch
            optionFilterProp="label"
            options={employeeOptions}
            loading={employeesLoading}
            placeholder="Select an employee (optional)"
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default EditUserForm
