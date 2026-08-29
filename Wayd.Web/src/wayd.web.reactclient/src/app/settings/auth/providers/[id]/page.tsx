'use client'

import { PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { useGetOidcProviderQuery } from '@/src/store/features/user-management/oidc-providers-api'
import { Tag } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { notFound, useRouter } from 'next/navigation'
import { use, useEffect, useState } from 'react'
import {
  ActiveTenantMigrations,
  DeleteOidcProviderForm,
  EditOidcProviderForm,
  OidcProviderDetails,
  StageTenantMigrationForm,
} from '../_components'
import OidcProviderDetailsLoading from './loading'
import getTenantMigrationAccess from './_components/tenant-migration-access'

enum ProviderSections {
  Overview = 'overview',
  ActiveMigrations = 'active-migrations',
}

/** The dialogs this record can open. One value, not one boolean each. */
type DialogId = 'edit' | 'delete' | 'stage-migration'

const OidcProviderDetailsPage = (props: {
  params: Promise<{ id: string }>
}) => {
  const { id } = use(props.params)
  const router = useRouter()

  const [dialog, setDialog] = useState<DialogId | null>(null)

  const { data: provider, isLoading, error } = useGetOidcProviderQuery(id)

  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.OidcProviders.Update')
  const canDelete = hasPermissionClaim('Permissions.OidcProviders.Delete')
  const canViewUsers = hasPermissionClaim('Permissions.Users.View')
  const canStageMigration = hasPermissionClaim('Permissions.Users.Update')

  const { canMigrateUsers, showActiveMigrations } = getTenantMigrationAccess({
    provider,
    canViewUsers,
    canStageMigration,
  })

  useDocumentTitle(
    provider
      ? `${provider.displayName} - Identity Provider`
      : 'Identity Provider',
  )

  useEffect(() => {
    error && console.error(error)
  }, [error])

  const actionsMenuItems: ItemType[] = (() => {
    if (!provider) return []

    const items: ItemType[] = []
    if (canUpdate) {
      items.push({
        key: 'edit',
        label: 'Edit',
        onClick: () => setDialog('edit'),
      })
    }
    if (canMigrateUsers) {
      items.push({
        key: 'migrate-tenant',
        label: 'Migrate Users to New Tenant',
        onClick: () => setDialog('stage-migration'),
      })
    }
    if (canDelete) {
      items.push({
        key: 'delete',
        label: 'Delete',
        danger: true,
        onClick: () => setDialog('delete'),
      })
    }
    return items
  })()

  // Active Migrations exists only for a multi-tenant Entra provider the viewer
  // can see users on. Everywhere else that leaves one section, and
  // `RecordLayout` drops the rail rather than spending 190px on it.
  const sections: RecordSection[] = [
    { id: ProviderSections.Overview, label: 'Overview' },
    ...(showActiveMigrations
      ? [
          {
            id: ProviderSections.ActiveMigrations,
            label: 'Active Migrations',
          },
        ]
      : []),
  ]

  if (isLoading) {
    return <OidcProviderDetailsLoading />
  }

  if (!provider) {
    return notFound()
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={ProviderSections.Overview}
        record={{
          name: provider.displayName,
          parent: {
            label: 'Identity Providers',
            href: '/settings/auth/providers',
          },
          subtitle: 'Identity Provider',
          tags: provider.isEnabled ? (
            <Tag color="success">Enabled</Tag>
          ) : (
            <Tag color="default">Disabled</Tag>
          ),
          actions:
            actionsMenuItems.length > 0 ? (
              <PageActions actionItems={actionsMenuItems} />
            ) : undefined,
        }}
      >
        {(section) =>
          section === ProviderSections.ActiveMigrations ? (
            <ActiveTenantMigrations providerId={provider.id} />
          ) : (
            <OidcProviderDetails provider={provider} />
          )
        }
      </RecordLayout>

      {dialog === 'edit' && (
        <EditOidcProviderForm
          providerId={provider.id}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'delete' && (
        <DeleteOidcProviderForm
          provider={provider}
          onFormComplete={() => {
            setDialog(null)
            router.push('/settings/auth/providers')
          }}
          onFormCancel={() => setDialog(null)}
        />
      )}
      {dialog === 'stage-migration' && (
        <StageTenantMigrationForm
          providerId={provider.id}
          allowedTenantIds={provider.allowedTenantIds ?? []}
          onFormComplete={() => setDialog(null)}
          onFormCancel={() => setDialog(null)}
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
