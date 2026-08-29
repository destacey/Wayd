'use client'

import { InactiveTag, PageActions } from '@/src/components/common'
import { personInitials } from '@/src/components/common/record-initials'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetAuthProvidersQuery } from '@/src/store/features/common/auth-providers-api'
import {
  useCancelProviderMigrationMutation,
  useCancelTenantMigrationMutation,
  useGetUserQuery,
} from '@/src/store/features/user-management/users-api'
import { App, Space, Tag } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { notFound } from 'next/navigation'
import { use, useEffect, useState } from 'react'
import {
  ConvertToLocalAccountForm,
  EditUserForm,
  ManageUserRolesForm,
  ResetPasswordForm,
  StageProviderMigrationForm,
  useUserAccountActions,
} from '../_components'
import SettingsRecordShell from '../../../_components/settings-record-shell'
import UserDetailsLoading from './loading'
import { UserOverview } from './_components'

enum UserSections {
  Overview = 'overview',
}

/** The dialogs this record can open. One value, not one boolean each. */
type DialogId =
  | 'edit'
  | 'manage-roles'
  | 'reset-password'
  | 'stage-provider-migration'
  | 'convert-to-local'

const UserDetailsPage = (props: { params: Promise<{ id: string }> }) => {
  const { id } = use(props.params)

  const [dialog, setDialog] = useState<DialogId | null>(null)

  const { hasPermissionClaim } = useAuth()
  const canUpdateUser = hasPermissionClaim('Permissions.Users.Update')
  const canUpdateUserRoles = hasPermissionClaim('Permissions.UserRoles.Update')

  const { getAccountActionMenuItems } = useUserAccountActions()
  const { data: user, isLoading, error } = useGetUserQuery(id)
  const [cancelTenantMigration] = useCancelTenantMigrationMutation()
  const [cancelProviderMigration] = useCancelProviderMigrationMutation()
  const { data: authProviders } = useGetAuthProvidersQuery()
  const oidcProviders = authProviders?.oidc ?? []
  const { modal } = App.useApp()
  const messageApi = useMessage()

  const isLocalUser = user?.loginProvider === 'Wayd'
  const isEntraUser = user?.loginProvider === 'MicrosoftEntraId'
  const isOidcUser = !isLocalUser
  const hasPendingMigration = !!user?.pendingMigrationTenantId
  const hasPendingProviderMigration = !!user?.pendingMigrationProviderId
  // Only offer "Change Identity Provider" when an enabled OIDC provider exists
  // that differs from the user's current one. The public auth/providers
  // endpoint returns only enabled providers, so no extra filter is needed.
  const canChangeProvider =
    oidcProviders.length > 0 &&
    oidcProviders.some((p) => p.name !== user?.loginProvider)

  const fullName = user ? `${user.firstName} ${user.lastName}` : ''
  useDocumentTitle(user ? `${fullName} - User Details` : 'User Details')

  useEffect(() => {
    error && console.error(error)
  }, [error])

  const actionsMenuItems: ItemType[] = (() => {
    if (!user) return []

    const items: ItemType[] = []
    if (canUpdateUser) {
      items.push({
        key: 'edit',
        label: 'Edit',
        onClick: () => setDialog('edit'),
      })
      items.push(
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
    if (canUpdateUser && isLocalUser) {
      secondaryItems.push({
        key: 'reset-password',
        label: 'Reset Password',
        onClick: () => setDialog('reset-password'),
      })
    }
    // Staging a tenant migration is a bulk action on the provider page.
    // Cancelling one user's pending migration stays here, where the pending
    // state is visible.
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
                const result = await cancelTenantMigration(user.id)
                if ('error' in result) {
                  throw result.error
                }
                messageApi.success('Pending migration canceled.')
              } catch (err: any) {
                messageApi.error(
                  err?.data?.detail ?? 'Failed to cancel the pending migration.',
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
        onClick: () => setDialog('stage-provider-migration'),
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
                  const result = await cancelProviderMigration(user.id)
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
        onClick: () => setDialog('convert-to-local'),
      })
    }
    if (canUpdateUserRoles) {
      secondaryItems.push({
        key: 'manage-roles',
        label: 'Manage Roles',
        onClick: () => setDialog('manage-roles'),
      })
    }
    if (secondaryItems.length > 0 && items.length > 0) {
      items.push({ key: 'divider', type: 'divider' })
    }
    items.push(...secondaryItems)
    return items
  })()

  // One section, so no rail — and no facts panel, which is closed by default
  // and holds reference material beside content. An account's own fields are
  // what the page is for, so they and its history stack on the one page.
  const sections: RecordSection[] = [
    { id: UserSections.Overview, label: 'Overview' },
  ]

  if (isLoading) {
    return <UserDetailsLoading />
  }

  if (!user) {
    return notFound()
  }

  // A staged migration is a state of the account, so it reads in the identity
  // bar beside the active tag rather than as a banner one section owns — it
  // stays visible whichever section is open, and disappears when the rebind
  // completes on the user's next sign-in.
  const tags = (
    <Space size={4} wrap>
      <InactiveTag isActive={user.isActive} />
      {hasPendingMigration && <Tag color="processing">Tenant migration pending</Tag>}
      {hasPendingProviderMigration && (
        <Tag color="processing">Provider migration pending</Tag>
      )}
    </Space>
  )

  return (
    <SettingsRecordShell>
      <RecordLayout
        sections={sections}
        defaultSection={UserSections.Overview}
        record={{
          name: fullName,
          avatar: {
            initials: personInitials(user.firstName, user.lastName),
          },
          parent: {
            label: 'Users',
            href: '/settings/user-management/users',
          },
          subtitle: 'User Details',
          tags,
          actions:
            actionsMenuItems.length > 0 ? (
              <PageActions actionItems={actionsMenuItems} />
            ) : undefined,
        }}
      >
        {() => <UserOverview user={user} />}
      </RecordLayout>

      {dialog === 'edit' && (
        <EditUserForm
          user={user}
          onFormUpdate={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'manage-roles' && (
        <ManageUserRolesForm
          userId={user.id}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'reset-password' && (
        <ResetPasswordForm
          userId={user.id}
          userName={fullName}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'stage-provider-migration' && (
        <StageProviderMigrationForm
          userId={user.id}
          userName={fullName}
          currentLoginProvider={user.loginProvider}
          currentPendingProviderId={user.pendingMigrationProviderId}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'convert-to-local' && (
        <ConvertToLocalAccountForm
          userId={user.id}
          userName={fullName}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
    </SettingsRecordShell>
  )
}

const UserDetailsPageWithAuthorization = authorizePage(
  UserDetailsPage,
  'Permission',
  'Permissions.Users.View',
)

export default UserDetailsPageWithAuthorization
