'use client'

import { Form, Input, Modal, Select } from 'antd'
import { toFormErrors } from '@/src/utils'
import {
  useCreateUserMutation,
  useGetUsersQuery,
} from '@/src/store/features/user-management/users-api'
import { useGetRolesQuery } from '@/src/store/features/user-management/roles-api'
import { useGetEmployeesQuery } from '@/src/store/features/organizations/employee-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useModalForm } from '@/src/hooks'

const { Item } = Form

const loginProviderOptions = [
  { value: 'MicrosoftEntraId', label: 'Microsoft Entra ID' },
  { value: 'Wayd', label: 'Wayd' },
]

export interface CreateUserFormProps {
  onFormCreate: () => void
  onFormCancel: () => void
}

interface CreateUserFormValues {
  firstName: string
  lastName: string
  email: string
  phoneNumber?: string
  loginProvider: string
  password?: string
  employeeId?: string
  roles?: string[]
}

const CreateUserForm = ({
  onFormCreate,
  onFormCancel,
}: CreateUserFormProps) => {
  const messageApi = useMessage()
  const [createUser] = useCreateUserMutation()
  const { data: rolesData, isLoading: rolesLoading } = useGetRolesQuery()
  const { data: employeesData, isLoading: employeesLoading } =
    useGetEmployeesQuery(false)
  const { data: usersData } = useGetUsersQuery()

  const roleOptions = rolesData?.map((role) => ({
        value: role.name,
        label: role.name,
      })) ?? []

  const employeeOptions = (() => {
    if (!employeesData) return []
    const linkedEmployeeIds = new Set(
      usersData
        ?.filter((u) => u.employee?.id)
        .map((u) => u.employee!.id) ?? [],
    )
    return employeesData
      .filter((e) => e.isActive && !linkedEmployeeIds.has(e.id))
      .map((e) => ({ value: e.id, label: e.displayName }))
  })()

  const { form, isOpen, isValid, isSaving, handleOk, handleCancel } =
    useModalForm<CreateUserFormValues>({
      onSubmit: async (values: CreateUserFormValues, form) => {
          try {
            const response = await createUser({
              firstName: values.firstName,
              lastName: values.lastName,
              email: values.email,
              phoneNumber: values.phoneNumber || undefined,
              employeeId: values.employeeId || undefined,
              loginProvider: values.loginProvider,
              password:
                values.loginProvider === 'Wayd'
                  ? values.password
                  : undefined,
              roleNames: values.roles ?? [],
            })

            if (response.error) {
              throw response.error
            }

            messageApi.success('Successfully created user.')
            onFormCreate()
            return false
          } catch (error: any) {
            if (error.status === 422 && error.errors) {
              const formErrors = toFormErrors(error.errors)
              form.setFields(formErrors)
              messageApi.error('Correct the validation error(s) to continue.')
            } else {
              messageApi.error(
                error.detail ??
                  'An unexpected error occurred while creating the user.',
              )
            }
            return false
          }
        },
      onComplete: () => {},
      onCancel: onFormCancel,
      errorMessage: 'An unexpected error occurred while creating the user.',
      permission: 'Permissions.Users.Create',
    })

  const loginProvider = Form.useWatch('loginProvider', form)

  return (
    <Modal
      title="Create User"
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
        name="create-user-form"
        initialValues={{ loginProvider: 'MicrosoftEntraId' }}
        autoComplete="off"
      >
        <Item
          label="Login Provider"
          name="loginProvider"
          rules={[{ required: true, message: 'Login provider is required' }]}
        >
          <Select options={loginProviderOptions} />
        </Item>

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

        {loginProvider === 'Wayd' && (
          <Item
            label="Password"
            name="password"
            rules={[
              {
                required: true,
                message: 'Password is required for Wayd accounts',
              },
              { min: 8, message: 'Password must be at least 8 characters' },
              {
                pattern: /[A-Z]/,
                message: 'Password must contain at least one uppercase letter',
              },
              {
                pattern: /[a-z]/,
                message: 'Password must contain at least one lowercase letter',
              },
              {
                pattern: /[0-9]/,
                message: 'Password must contain at least one digit',
              },
            ]}
          >
            <Input.Password maxLength={128} autoComplete="new-password" />
          </Item>
        )}

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

        <Item
          label="Roles"
          name="roles"
          rules={[{ required: true, message: 'At least one role is required' }]}
        >
          <Select
            mode="multiple"
            options={roleOptions}
            loading={rolesLoading}
            placeholder="Select one or more roles"
          />
        </Item>
      </Form>
    </Modal>
  )
}

export default CreateUserForm
