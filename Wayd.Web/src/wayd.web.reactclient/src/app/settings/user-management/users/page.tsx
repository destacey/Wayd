'use client'

import PageTitle from '@/src/components/common/page-title'
import { useEffect, useMemo, useState } from 'react'
import {
  WaydGrid,
  createActionsColumn,
  renderUserLink,
  formatDateTime,
} from '@/src/components/common/wayd-grid'
import { authorizePage } from '@/src/components/hoc'
import Link from 'next/link'
import { Button, Tag } from 'antd'
import { WaydTooltip } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { useDocumentTitle } from '@/src/hooks'
import { useGetUsersQuery } from '@/src/store/features/user-management/users-api'
import { UserDetailsDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@tanstack/react-table'
import dayjs from 'dayjs'
import {
  CreateUserForm,
  EditUserForm,
  ManageUserRolesForm,
  ResetPasswordForm,
  useUserAccountActions,
} from './_components'
import { ItemType } from 'antd/es/menu/interface'

const UsersListPage = () => {
  useDocumentTitle('Users')
  const [openCreateUserForm, setOpenCreateUserForm] = useState(false)
  const [editingUser, setEditingUser] = useState<UserDetailsDto | null>(null)
  const [managingRolesUserId, setManagingRolesUserId] = useState<string | null>(
    null,
  )
  const [resettingPasswordUser, setResettingPasswordUser] =
    useState<UserDetailsDto | null>(null)

  const { hasClaim } = useAuth()
  const canCreateUser = hasClaim('Permission', 'Permissions.Users.Create')
  const canUpdateUser = hasClaim('Permission', 'Permissions.Users.Update')
  const canUpdateUserRoles = hasClaim(
    'Permission',
    'Permissions.UserRoles.Update',
  )
  const showRowActions = canUpdateUser || canUpdateUserRoles

  const { getAccountActionMenuItems } = useUserAccountActions()
  const { data: usersData, isLoading, error, refetch } = useGetUsersQuery()

  const columns = useMemo<ColumnDef<UserDetailsDto, any>[]>(
    () => [
      createActionsColumn<UserDetailsDto>({
        hide: !showRowActions,
        ariaLabel: 'User actions',
        getItems: (user) => {
          const menuItems: ItemType[] = []
          if (canUpdateUser) {
            menuItems.push({
              key: 'edit',
              label: 'Edit',
              onClick: () => setEditingUser(user),
            })
            menuItems.push(
              ...getAccountActionMenuItems({
                id: user.id,
                userName: user.userName!,
                firstName: user.firstName!,
                lastName: user.lastName!,
                isActive: user.isActive,
                isLockedOut:
                  !!user.lockoutEnd && new Date(user.lockoutEnd) > new Date(),
              }),
            )
          }
          const secondaryItems: ItemType[] = []
          if (canUpdateUser && user.loginProvider === 'Wayd') {
            secondaryItems.push({
              key: 'reset-password',
              label: 'Reset Password',
              onClick: () => setResettingPasswordUser(user),
            })
          }
          if (canUpdateUserRoles) {
            secondaryItems.push({
              key: 'manage-roles',
              label: 'Manage Roles',
              onClick: () => setManagingRolesUserId(user.id),
            })
          }
          if (secondaryItems.length > 0 && menuItems.length > 0) {
            menuItems.push({ key: 'divider', type: 'divider' })
          }
          menuItems.push(...secondaryItems)
          return menuItems
        },
      }),
      {
        id: 'userName',
        accessorKey: 'userName',
        header: 'User Name',
        cell: ({ row }) => renderUserLink(row.original),
      },
      { id: 'firstName', accessorKey: 'firstName', header: 'First Name' },
      { id: 'lastName', accessorKey: 'lastName', header: 'Last Name' },
      { id: 'email', accessorKey: 'email', header: 'Email' },
      {
        id: 'loginProvider',
        accessorFn: (row) =>
          row.loginProvider === 'MicrosoftEntraId'
            ? 'Microsoft Entra ID'
            : (row.loginProvider ?? ''),
        header: 'Login Provider',
        meta: { filterType: 'set' },
      },
      {
        id: 'employee',
        accessorKey: 'employee.name',
        header: 'Employee',
        cell: ({ row }) =>
          row.original.employee ? (
            <Link
              href={`/organizations/employees/${row.original.employee.key}`}
            >
              {row.original.employee.name}
            </Link>
          ) : null,
      },
      {
        id: 'roles',
        accessorFn: (row) =>
          row.roles
            ?.map((r) => r.name)
            .sort()
            .join(', ') ?? '',
        header: 'Roles',
      },
      {
        id: 'lockoutEnd',
        accessorKey: 'lockoutEnd',
        header: 'Locked Out',
        size: 120,
        cell: ({ getValue }) => {
          const value = getValue() as Date | undefined
          return value && new Date(value) > new Date() ? (
            <WaydTooltip
              title={`Locked until ${dayjs(value).format('MMM D, YYYY h:mm A')}`}
            >
              <Tag color="error">Locked</Tag>
            </WaydTooltip>
          ) : null
        },
      },
      {
        id: 'lastActivityAt',
        accessorKey: 'lastActivityAt',
        header: 'Last Activity',
        meta: { columnType: 'dateTime' },
        cell: ({ getValue }) => (getValue() ? formatDateTime(getValue()) : ''),
      },
      {
        id: 'isActive',
        accessorFn: (row) => (row.isActive ? 'Active' : 'Inactive'),
        header: 'Active',
        size: 100,
        meta: { filterType: 'set' },
        cell: ({ row }) =>
          row.original.isActive ? (
            <Tag color="success">Active</Tag>
          ) : (
            <Tag color="error">Inactive</Tag>
          ),
      },
    ],
    [
      showRowActions,
      canUpdateUser,
      canUpdateUserRoles,
      getAccountActionMenuItems,
    ],
  )

  useEffect(() => {
    error && console.error(error)
  }, [error])

  const refresh = async () => {
    refetch()
  }

  const actions = () => {
    return (
      <>
        {canCreateUser && (
          <Button onClick={() => setOpenCreateUserForm(true)}>
            Create User
          </Button>
        )}
      </>
    )
  }

  return (
    <>
      <PageTitle title="Users" actions={actions()} />

      <WaydGrid
        columns={columns}
        data={usersData ?? []}
        onRefresh={refresh}
        isLoading={isLoading}
        csvFileName="users"
      />

      {openCreateUserForm && (
        <CreateUserForm
          onFormCreate={() => {
            setOpenCreateUserForm(false)
            refetch()
          }}
          onFormCancel={() => setOpenCreateUserForm(false)}
        />
      )}
      {editingUser && (
        <EditUserForm
          user={editingUser}
          onFormUpdate={() => setEditingUser(null)}
          onFormCancel={() => setEditingUser(null)}
        />
      )}
      {managingRolesUserId && (
        <ManageUserRolesForm
          userId={managingRolesUserId}
          onFormComplete={() => setManagingRolesUserId(null)}
          onFormCancel={() => setManagingRolesUserId(null)}
        />
      )}
      {resettingPasswordUser && (
        <ResetPasswordForm
          userId={resettingPasswordUser.id}
          userName={`${resettingPasswordUser.firstName} ${resettingPasswordUser.lastName}`}
          onFormComplete={() => setResettingPasswordUser(null)}
          onFormCancel={() => setResettingPasswordUser(null)}
        />
      )}
    </>
  )
}

const PageWithAuthorization = authorizePage(
  UsersListPage,
  'Permission',
  'Permissions.Users.View',
)

export default PageWithAuthorization
