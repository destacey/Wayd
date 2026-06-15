'use client'

import { PageActions } from '@/src/components/common'
import PageTitle from '@/src/components/common/page-title'
import BasicBreadcrumb from '@/src/components/common/basic-breadcrumb'
import { authorizePage } from '@/src/components/hoc'
import useAuth from '@/src/components/contexts/auth'
import { useDocumentTitle } from '@/src/hooks'
import { useGetOidcProviderQuery } from '@/src/store/features/user-management/oidc-providers-api'
import { OidcProviderType } from '@/src/services/wayd-api'
import { ItemType } from 'antd/es/menu/interface'
import { Card, MenuProps, Skeleton, Tag } from 'antd'
import { notFound, useRouter } from 'next/navigation'
import { use, useEffect, useState } from 'react'
import {
  ActiveTenantMigrations,
  DeleteOidcProviderForm,
  EditOidcProviderForm,
  OidcProviderDetails,
  StageTenantMigrationForm,
} from '../_components'

enum ProviderDetailsTabs {
  Details = 'details',
  ActiveMigrations = 'active-migrations',
}

const OidcProviderDetailsPage = (props: {
  params: Promise<{ id: string }>
}) => {
  const { id } = use(props.params)
  const router = useRouter()

  const [openEditForm, setOpenEditForm] = useState(false)
  const [openDeleteForm, setOpenDeleteForm] = useState(false)
  const [openStageMigrationForm, setOpenStageMigrationForm] = useState(false)
  const [activeTab, setActiveTab] = useState(ProviderDetailsTabs.Details)

  const { data: provider, isLoading, error } = useGetOidcProviderQuery(id)

  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.OidcProviders.Update')
  const canDelete = hasPermissionClaim('Permissions.OidcProviders.Delete')
  const canViewUsers = hasPermissionClaim('Permissions.Users.View')
  const canStageMigration = hasPermissionClaim('Permissions.Users.Update')

  // Tenant migration only applies to a multi-tenant Entra provider — there must be
  // at least two allowed tenants to move users between.
  const isMultiTenantEntra =
    provider?.providerType === OidcProviderType.MicrosoftEntraId &&
    (provider?.allowedTenantIds?.length ?? 0) >= 2

  // The "Migrate" action stages a migration (Users.Update); the Active Migrations tab
  // is a read-only view (Users.View).
  const canMigrateUsers = isMultiTenantEntra && canStageMigration
  const showActiveMigrationsTab = isMultiTenantEntra && canViewUsers

  const title = provider
    ? `${provider.displayName} - Identity Provider`
    : 'Identity Provider'
  useDocumentTitle(title)

  useEffect(() => {
    error && console.error(error)
  }, [error])

  const actionsMenuItems: MenuProps['items'] = (() => {
    if (!provider) return []
    const items: ItemType[] = []
    if (canUpdate) {
      items.push({
        key: 'edit',
        label: 'Edit',
        onClick: () => setOpenEditForm(true),
      })
    }
    if (canMigrateUsers) {
      items.push({
        key: 'migrate-tenant',
        label: 'Migrate Users to New Tenant',
        onClick: () => setOpenStageMigrationForm(true),
      })
    }
    if (canDelete) {
      items.push({
        key: 'delete',
        label: 'Delete',
        danger: true,
        onClick: () => setOpenDeleteForm(true),
      })
    }
    return items
  })()

  if (isLoading) {
    return (
      <Card style={{ width: '100%' }}>
        <Skeleton active />
      </Card>
    )
  }

  if (!provider) {
    return notFound()
  }

  return (
    <>
      <BasicBreadcrumb
        items={[
          { title: 'Settings' },
          { title: 'Identity Providers', href: './' },
          { title: 'Details' },
        ]}
      />
      <PageTitle
        title={provider.displayName}
        subtitle="Identity Provider"
        tags={
          provider.isEnabled ? (
            <Tag color="success">Enabled</Tag>
          ) : (
            <Tag color="default">Disabled</Tag>
          )
        }
        actions={<PageActions actionItems={actionsMenuItems} />}
      />
      {showActiveMigrationsTab ? (
        <Card
          style={{ width: '100%' }}
          tabList={[
            { key: ProviderDetailsTabs.Details, tab: 'Details' },
            {
              key: ProviderDetailsTabs.ActiveMigrations,
              tab: 'Active Migrations',
            },
          ]}
          activeTabKey={activeTab}
          onTabChange={(key) => setActiveTab(key as ProviderDetailsTabs)}
        >
          {activeTab === ProviderDetailsTabs.Details ? (
            <OidcProviderDetails provider={provider} />
          ) : (
            <ActiveTenantMigrations providerId={provider.id} />
          )}
        </Card>
      ) : (
        <Card style={{ width: '100%' }}>
          <OidcProviderDetails provider={provider} />
        </Card>
      )}

      {openEditForm && (
        <EditOidcProviderForm
          providerId={provider.id}
          onFormComplete={() => setOpenEditForm(false)}
          onFormCancel={() => setOpenEditForm(false)}
        />
      )}
      {openDeleteForm && (
        <DeleteOidcProviderForm
          provider={provider}
          onFormComplete={() => {
            setOpenDeleteForm(false)
            router.push('/settings/auth/providers')
          }}
          onFormCancel={() => setOpenDeleteForm(false)}
        />
      )}
      {openStageMigrationForm && (
        <StageTenantMigrationForm
          providerId={provider.id}
          allowedTenantIds={provider.allowedTenantIds ?? []}
          onFormComplete={() => setOpenStageMigrationForm(false)}
          onFormCancel={() => setOpenStageMigrationForm(false)}
        />
      )}
    </>
  )
}

const OidcProviderDetailsPageWithAuthorization = authorizePage(
  OidcProviderDetailsPage,
  'Permission',
  'Permissions.OidcProviders.View',
)

export default OidcProviderDetailsPageWithAuthorization
