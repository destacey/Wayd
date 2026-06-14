'use client'

import { PageActions, PageTitle } from '@/src/components/common'
import { Card, Flex, MenuProps, Spin, Tag, Typography } from 'antd'
import { use, useState } from 'react'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { notFound, useRouter } from 'next/navigation'
import BasicBreadcrumb from '@/src/components/common/basic-breadcrumb'
import {
  useGetRoleQuery,
  useGetRoleUsersCountQuery,
} from '@/src/store/features/user-management/roles-api'
import {
  DeleteRoleForm,
  EditRoleForm,
  ManageRoleUsersForm,
  Permissions,
  RoleUsersGrid,
} from '../_components'
import { ItemType } from 'antd/es/menu/interface'
import { useDocumentTitle } from '@/src/hooks'
import { TeamOutlined } from '@ant-design/icons'

const { Text } = Typography

enum RoleDetailsTabs {
  Permissions = 'permissions',
  Users = 'users',
}

const getRoleTabs = () => [
  {
    key: RoleDetailsTabs.Permissions,
    tab: 'Permissions',
  },
  {
    key: RoleDetailsTabs.Users,
    tab: 'Users',
  },
]

const RoleDetailsPage = (props: { params: Promise<{ id: string }> }) => {
  const { id } = use(props.params)

  const [activeTab, setActiveTab] = useState(RoleDetailsTabs.Permissions)
  const [permissionsDirty, setPermissionsDirty] = useState(false)
  const [openEditRoleForm, setOpenEditRoleForm] = useState<boolean>(false)
  const [openDeleteRoleForm, setOpenDeleteRoleForm] = useState<boolean>(false)
  const [openManageRoleUsersForm, setOpenManageRoleUsersForm] =
    useState<boolean>(false)

  const router = useRouter()

  const { hasPermissionClaim } = useAuth()

  const {
    data: roleData,
    isLoading: isLoading,
    error: error,
    refetch: refetch,
  } = useGetRoleQuery(id)

  const { data: countData } = useGetRoleUsersCountQuery(id)

  const title = roleData ? `${roleData.name} - Role Details` : 'Role Details'
  useDocumentTitle(title)

  const isSystemRole = !!roleData && roleData.name === 'Admin'
  const editableRole = !!roleData && !isSystemRole

  const canUpdateRole =
    hasPermissionClaim('Permissions.Roles.Update') && editableRole
  const canDeleteRole =
    hasPermissionClaim('Permissions.Roles.Delete') && editableRole
  const canUpdateUserRoles = hasPermissionClaim('Permissions.UserRoles.Update')

  const tabs = getRoleTabs()

  const actionsMenuItems: MenuProps['items'] = (() => {
    let includesDetailsSection = false
    const items: ItemType[] = []
    if (canUpdateRole) {
      items.push({
        key: 'edit',
        label: 'Edit',
        onClick: () => setOpenEditRoleForm(true),
      })
      includesDetailsSection = true
    }
    if (canDeleteRole) {
      items.push({
        key: 'delete',
        label: 'Delete',
        onClick: () => setOpenDeleteRoleForm(true),
        disabled: countData !== undefined && countData > 0,
        title:
          countData !== undefined && countData > 0
            ? 'This role is assigned to users and cannot be deleted.'
            : undefined,
      })
      includesDetailsSection = true
    }
    if (canUpdateUserRoles) {
      if (includesDetailsSection) {
        items.push({
          key: 'manage-divider',
          type: 'divider',
        })
      }
      items.push({
        key: 'manage-users',
        label: 'Manage Users',
        onClick: () => setOpenManageRoleUsersForm(true),
      })
    }
    return items
  })()

  const renderTabContent = () => {
    switch (activeTab) {
      case RoleDetailsTabs.Permissions:
        return (
          <Permissions
            role={roleData!}
            permissions={roleData?.permissions ?? []}
            isSystemRole={isSystemRole}
            onDirtyChange={setPermissionsDirty}
          />
        )
      case RoleDetailsTabs.Users:
        return <RoleUsersGrid roleId={id} />
      default:
        return null
    }
  }

  const onTabChange = (tabKey: string) => {
    if (
      permissionsDirty &&
      !window.confirm(
        'You have unsaved permission changes. Are you sure you want to leave?',
      )
    )
      return
    setPermissionsDirty(false)
    setActiveTab(tabKey as RoleDetailsTabs)
  }

  const onDeleteRoleFormClosed = (wasDeleted: boolean) => {
    setOpenDeleteRoleForm(false)
    if (wasDeleted) {
      router.push('/settings/user-management/roles')
    }
  }

  if (!isLoading && !roleData) {
    return notFound()
  }

  return (
    <>
      <BasicBreadcrumb
        items={[
          { title: 'Settings' },
          { title: 'User Management' },
          { title: 'Roles', href: './' },
          { title: 'Details' },
        ]}
      />
      <PageTitle
        title={roleData?.name}
        subtitle="Role Details"
        tags={isSystemRole ? <Tag color="warning">System Role</Tag> : undefined}
        actions={<PageActions actionItems={actionsMenuItems} />}
        extra={
          <Flex vertical gap="small">
            {roleData?.description && (
              <Text type="secondary">{roleData?.description}</Text>
            )}
            {countData !== undefined && (
              <Text type="secondary">
                <TeamOutlined style={{ marginRight: 4 }} />
                {countData} user{countData !== 1 ? 's' : ''} assigned
              </Text>
            )}
          </Flex>
        }
      />
      <Card tabList={tabs} activeTabKey={activeTab} onTabChange={onTabChange}>
        <Spin spinning={isLoading}>{!isLoading && renderTabContent()}</Spin>
      </Card>
      {openEditRoleForm && (
        <EditRoleForm
          role={roleData!}
          onFormComplete={() => {
            setOpenEditRoleForm(false)
            refetch()
          }}
          onFormCancel={() => setOpenEditRoleForm(false)}
        />
      )}
      {openManageRoleUsersForm && roleData && (
        <ManageRoleUsersForm
          roleId={id}
          roleName={roleData.name}
          onFormComplete={() => setOpenManageRoleUsersForm(false)}
          onFormCancel={() => setOpenManageRoleUsersForm(false)}
        />
      )}
      {openDeleteRoleForm && (
        <DeleteRoleForm
          role={roleData!}
          onFormComplete={() => onDeleteRoleFormClosed(true)}
          onFormCancel={() => onDeleteRoleFormClosed(false)}
        />
      )}
    </>
  )
}

const RoleDetailsPageWithAuthorization = authorizePage(
  RoleDetailsPage,
  'Permission',
  'Permissions.Roles.View',
)

export default RoleDetailsPageWithAuthorization

