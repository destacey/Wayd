'use client'

import { PageActions } from '@/src/components/common'
import PageTitle from '@/src/components/common/page-title'
import { authorizePage } from '@/src/components/hoc'
import { notFound } from 'next/navigation'
import UserDetailsLoading from './loading'
import { use, useEffect, useState } from 'react'
import { App, Card, MenuProps } from 'antd'
import BasicBreadcrumb from '@/src/components/common/basic-breadcrumb'
import useAuth from '@/src/components/contexts/auth'
import {
  useCancelProviderMigrationMutation,
  useCancelTenantMigrationMutation,
  useGetUserQuery,
} from '@/src/store/features/user-management/users-api'
import { useGetAuthProvidersQuery } from '@/src/store/features/common/auth-providers-api'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  ConvertToLocalAccountForm,
  EditUserForm,
  ManageUserRolesForm,
  ResetPasswordForm,
  StageProviderMigrationForm,
  useUserAccountActions,
  UserDetails,
  UserIdentityHistory,
} from '../_components'
import { ItemType } from 'antd/es/menu/interface'
import { useDocumentTitle } from '@/src/hooks'

enum UserDetailsTabs {
  Details = 'details',
  IdentityHistory = 'identity-history',
}

const UserDetailsPage = (props: { params: Promise<{ id: string }> }) => {
  const { id } = use(props.params)

  const [activeTab, setActiveTab] = useState(UserDetailsTabs.Details)
  const [openEditUserForm, setOpenEditUserForm] = useState(false)
  const [openManageUserRolesForm, setOpenManageUserRolesForm] = useState(false)
  const [openResetPasswordForm, setOpenResetPasswordForm] = useState(false)
  const [openStageProviderMigrationForm, setOpenStageProviderMigrationForm] =
    useState(false)
  const [openConvertToLocalForm, setOpenConvertToLocalForm] = useState(false)

  const { hasPermissionClaim } = useAuth()
  const canUpdateUser = hasPermissionClaim('Permissions.Users.Update')
  const canUpdateUserRoles = hasPermissionClaim('Permissions.UserRoles.Update')

  const { getAccountActionMenuItems } = useUserAccountActions()
  const { data: userData, isLoading, error } = useGetUserQuery(id)
  const [cancelTenantMigration] = useCancelTenantMigrationMutation()
  const [cancelProviderMigration] = useCancelProviderMigrationMutation()
  const { data: authProviders } = useGetAuthProvidersQuery()
  const oidcProviders = authProviders?.oidc ?? []
  const { modal } = App.useApp()
  const messageApi = useMessage()

  const isLocalUser = userData?.loginProvider === 'Wayd'
  const isEntraUser = userData?.loginProvider === 'MicrosoftEntraId'
  const isOidcUser = !isLocalUser
  const hasPendingMigration = !!userData?.pendingMigrationTenantId
  const hasPendingProviderMigration = !!userData?.pendingMigrationProviderId
  // Only show "Change Identity Provider" when at least one enabled OIDC provider
  // exists that is different from the user's current provider. The public
  // auth/providers endpoint only returns enabled providers, so no extra filter needed.
  const canChangeProvider =
    oidcProviders.length > 0 &&
    oidcProviders.some((p) => p.name !== userData?.loginProvider)

  const tabs = [
    { key: UserDetailsTabs.Details, tab: 'Details' },
    { key: UserDetailsTabs.IdentityHistory, tab: 'Identity History' },
  ]

  const fullName = userData
    ? `${userData?.firstName} ${userData?.lastName}`
    : ''
  const title = userData ? `${fullName} - User Details` : 'User Details'
  useDocumentTitle(title)

  const actionsMenuItems: MenuProps['items'] = (() => {
    if (!userData) return []

    const items: ItemType[] = []
    if (canUpdateUser) {
      items.push({
        key: 'edit',
        label: 'Edit',
        onClick: () => setOpenEditUserForm(true),
      })
      items.push(
        ...getAccountActionMenuItems({
          id: userData.id,
          userName: userData.userName!,
          firstName: userData.firstName!,
          lastName: userData.lastName!,
          isActive: userData.isActive,
          isLockedOut:
            !!userData.lockoutEnd && new Date(userData.lockoutEnd) > new Date(),
        }),
      )
    }
    const secondaryItems: ItemType[] = []
    if (canUpdateUser && isLocalUser) {
      secondaryItems.push({
        key: 'reset-password',
        label: 'Reset Password',
        onClick: () => setOpenResetPasswordForm(true),
      })
    }
    // Staging a tenant migration is a bulk action on the provider page. Cancelling a
    // single user's pending migration stays here, where the pending state is visible.
    if (canUpdateUser && isEntraUser && hasPendingMigration) {
      secondaryItems.push({
        key: 'cancel-migration',
        label: 'Cancel Pending Migration',
        onClick: () => {
          modal.confirm({
            title: 'Cancel Pending Migration',
            content: `Cancel the pending tenant migration for ${fullName}?`,
            okText: 'Cancel Migration',
            okButtonProps: { danger: true },
            cancelText: 'Keep Pending',
            onOk: async () => {
              try {
                const result = await cancelTenantMigration(userData.id)
                if ('error' in result) {
                  throw result.error
                }
                messageApi.success('Pending migration canceled.')
              } catch (err: any) {
                messageApi.error(
                  err?.data?.detail ??
                    'Failed to cancel the pending migration.',
                )
              }
            },
          })
        },
      })
    }
    if (canUpdateUser && canChangeProvider) {
      secondaryItems.push({
        key: 'change-provider',
        label: hasPendingProviderMigration
          ? 'Replace Pending Provider Migration'
          : 'Change Identity Provider',
        onClick: () => setOpenStageProviderMigrationForm(true),
      })
      if (hasPendingProviderMigration) {
        secondaryItems.push({
          key: 'cancel-provider-migration',
          label: 'Cancel Pending Provider Migration',
          onClick: () => {
            modal.confirm({
              title: 'Cancel Pending Provider Migration',
              content: `Cancel the pending provider migration for ${fullName}?`,
              okText: 'Cancel Migration',
              okButtonProps: { danger: true },
              cancelText: 'Keep Pending',
              onOk: async () => {
                try {
                  const result = await cancelProviderMigration(userData.id)
                  if ('error' in result) {
                    throw result.error
                  }
                  messageApi.success('Pending provider migration canceled.')
                } catch (err: any) {
                  messageApi.error(
                    err?.data?.detail ??
                      'Failed to cancel the pending provider migration.',
                  )
                }
              },
            })
          },
        })
      }
    }
    if (canUpdateUser && isOidcUser) {
      secondaryItems.push({
        key: 'convert-to-local',
        label: 'Convert to Local Account',
        onClick: () => setOpenConvertToLocalForm(true),
      })
    }
    if (canUpdateUserRoles) {
      secondaryItems.push({
        key: 'manage-roles',
        label: 'Manage Roles',
        onClick: () => setOpenManageUserRolesForm(true),
      })
    }
    if (secondaryItems.length > 0 && items.length > 0) {
      items.push({ key: 'divider', type: 'divider' })
    }
    items.push(...secondaryItems)
    return items
  })()

  const renderTabContent = () => {
    switch (activeTab) {
      case UserDetailsTabs.Details:
        return <UserDetails user={userData!} />
      case UserDetailsTabs.IdentityHistory:
        return <UserIdentityHistory userId={userData!.id} />
      default:
        return null
    }
  }

  const onTabChange = (tabKey: string) => {
    setActiveTab(tabKey as UserDetailsTabs)
  }

  useEffect(() => {
    error && console.error(error)
  }, [error])

  if (isLoading) {
    return <UserDetailsLoading />
  }

  if (!userData) {
    return notFound()
  }

  return (
    <>
      <BasicBreadcrumb
        items={[
          { title: 'Settings' },
          { title: 'User Management' },
          { title: 'Users', href: './' },
          { title: 'Details' },
        ]}
      />
      <PageTitle
        title={fullName}
        subtitle="User Details"
        actions={<PageActions actionItems={actionsMenuItems} />}
      />
      <Card
        style={{ width: '100%' }}
        tabList={tabs}
        activeTabKey={activeTab}
        onTabChange={onTabChange}
      >
        {renderTabContent()}
      </Card>
      {openEditUserForm && (
        <EditUserForm
          user={userData}
          onFormUpdate={() => setOpenEditUserForm(false)}
          onFormCancel={() => setOpenEditUserForm(false)}
        />
      )}
      {openManageUserRolesForm && (
        <ManageUserRolesForm
          userId={userData.id}
          onFormComplete={() => setOpenManageUserRolesForm(false)}
          onFormCancel={() => setOpenManageUserRolesForm(false)}
        />
      )}
      {openResetPasswordForm && (
        <ResetPasswordForm
          userId={userData.id}
          userName={fullName}
          onFormComplete={() => setOpenResetPasswordForm(false)}
          onFormCancel={() => setOpenResetPasswordForm(false)}
        />
      )}
      {openStageProviderMigrationForm && (
        <StageProviderMigrationForm
          userId={userData.id}
          userName={fullName}
          currentLoginProvider={userData.loginProvider}
          currentPendingProviderId={userData.pendingMigrationProviderId}
          onFormComplete={() => setOpenStageProviderMigrationForm(false)}
          onFormCancel={() => setOpenStageProviderMigrationForm(false)}
        />
      )}
      {openConvertToLocalForm && (
        <ConvertToLocalAccountForm
          userId={userData.id}
          userName={fullName}
          onFormComplete={() => setOpenConvertToLocalForm(false)}
          onFormCancel={() => setOpenConvertToLocalForm(false)}
        />
      )}
    </>
  )
}

const UserDetailsPageWithAuthorization = authorizePage(
  UserDetailsPage,
  'Permission',
  'Permissions.Users.View',
)

export default UserDetailsPageWithAuthorization
