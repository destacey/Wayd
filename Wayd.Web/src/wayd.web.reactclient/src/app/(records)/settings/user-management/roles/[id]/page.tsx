'use client'

import { PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  useGetRoleQuery,
  useGetRoleUsersCountQuery,
} from '@/src/store/features/user-management/roles-api'
import { Tag } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { notFound, useRouter } from 'next/navigation'
import { use, useState } from 'react'
import {
  DeleteRoleForm,
  EditRoleForm,
  ManageRoleUsersForm,
  Permissions,
  RoleUsersGrid,
} from '../_components'
import SettingsRecordShell from '../../../_components/settings-record-shell'
import RoleDetailsLoading from './loading'

enum RoleSections {
  Permissions = 'permissions',
  Users = 'users',
}

/** The dialogs this record can open. One value, not one boolean each. */
type DialogId = 'edit' | 'delete' | 'manage-users'

const RoleDetailsPage = (props: { params: Promise<{ id: string }> }) => {
  const { id } = use(props.params)

  const [dialog, setDialog] = useState<DialogId | null>(null)

  const router = useRouter()
  const { hasPermissionClaim } = useAuth()

  const {
    data: role,
    isLoading,
    refetch,
  } = useGetRoleQuery(id)

  const { data: userCount } = useGetRoleUsersCountQuery(id)

  useDocumentTitle(role ? `${role.name} - Role Details` : 'Role Details')

  // Admin is seeded and grants everything; editing or deleting it would lock
  // the product's own administrators out.
  const isSystemRole = !!role && role.name === 'Admin'
  const editableRole = !!role && !isSystemRole

  const canUpdateRole =
    hasPermissionClaim('Permissions.Roles.Update') && editableRole
  const canDeleteRole =
    hasPermissionClaim('Permissions.Roles.Delete') && editableRole
  const canUpdateUserRoles = hasPermissionClaim('Permissions.UserRoles.Update')

  const hasAssignedUsers = userCount !== undefined && userCount > 0

  const actionsMenuItems: ItemType[] = (() => {
    const items: ItemType[] = []

    if (canUpdateRole) {
      items.push({
        key: 'edit',
        label: 'Edit',
        onClick: () => setDialog('edit'),
      })
    }
    if (canDeleteRole) {
      items.push({
        key: 'delete',
        label: 'Delete',
        onClick: () => setDialog('delete'),
        disabled: hasAssignedUsers,
        title: hasAssignedUsers
          ? 'This role is assigned to users and cannot be deleted.'
          : undefined,
      })
    }
    if (canUpdateUserRoles) {
      if (items.length > 0) {
        items.push({ key: 'manage-divider', type: 'divider' })
      }
      items.push({
        key: 'manage-users',
        label: 'Manage Users',
        onClick: () => setDialog('manage-users'),
      })
    }

    return items
  })()

  // Both sections are substantial — a permission matrix and a user grid — so
  // this record keeps its rail, unlike the thinner settings records.
  const sections: RecordSection[] = [
    { id: RoleSections.Permissions, label: 'Permissions' },
    { id: RoleSections.Users, label: 'Users', count: userCount },
  ]

  const renderSection = (section: string) =>
    section === RoleSections.Users ? (
      <RoleUsersGrid roleId={id} />
    ) : (
      <Permissions
        role={role!}
        permissions={role?.permissions ?? []}
        isSystemRole={isSystemRole}
      />
    )

  if (isLoading) {
    return <RoleDetailsLoading />
  }

  if (!role) {
    return notFound()
  }

  return (
    <SettingsRecordShell>
      <RecordLayout
        sections={sections}
        defaultSection={RoleSections.Permissions}
        record={{
          name: role.name,
          parent: {
            label: 'Roles',
            href: '/settings/user-management/roles',
          },
          subtitle: 'Role Details',
          descriptor: role.description,
          tags: isSystemRole ? <Tag color="warning">System Role</Tag> : undefined,
          actions:
            actionsMenuItems.length > 0 ? (
              <PageActions actionItems={actionsMenuItems} />
            ) : undefined,
        }}
      >
        {(section) => renderSection(section)}
      </RecordLayout>

      {dialog === 'edit' && (
        <EditRoleForm
          role={role}
          onFormComplete={() => {
            setDialog(null)
            refetch()
          }}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'manage-users' && (
        <ManageRoleUsersForm
          roleId={id}
          roleName={role.name}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'delete' && (
        <DeleteRoleForm
          role={role}
          onFormComplete={() => {
            setDialog(null)
            router.push('/settings/user-management/roles')
          }}
          onFormCancel={() => setDialog(null)}
        />
      )}
    </SettingsRecordShell>
  )
}

const RoleDetailsPageWithAuthorization = authorizePage(
  RoleDetailsPage,
  'Permission',
  'Permissions.Roles.View',
)

export default RoleDetailsPageWithAuthorization
