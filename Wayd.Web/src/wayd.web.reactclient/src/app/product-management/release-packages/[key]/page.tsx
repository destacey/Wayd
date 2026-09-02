'use client'

import { PageActions } from '@/src/components/common'
import { RecordLayout } from '@/src/components/common/record'
import type { RecordSection } from '@/src/components/common/record'
import {
  StatusHistoryTag,
  StatusHistoryTimeline,
} from '@/src/components/common/status-workflows'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  useGetReleasePackageQuery,
  useGetReleasePackageStatusHistoryQuery,
} from '@/src/store/features/product-management/release-packages-api'
import { useGetDeploymentsQuery } from '@/src/store/features/product-management/deployments-api'
import { Button, MenuProps, Result } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { notFound, useRouter } from 'next/navigation'
import { use, useEffect, useState } from 'react'
import { DeploymentsGrid } from '../../deployments/_components'
import MarkReleasePackageReleasedForm from '../_components/mark-release-package-released-form'
import { releasePackageActionAvailability } from '../_components/release-package-actions'
import SetReleasePackageManifestForm from '../_components/set-release-package-manifest-form'
import WithdrawReleasePackageForm from '../_components/withdraw-release-package-form'
import ReleasePackageFacts from './_components/release-package-facts'
import ReleasePackageManifest from './_components/release-package-manifest'
import ReleasePackageDetailsLoading from './loading'

enum ReleasePackageSections {
  Manifest = 'manifest',
  Deployments = 'deployments',
  StatusHistory = 'status-history',
}

const ReleasePackageDetailsPage = (props: {
  params: Promise<{ key: string }>
}) => {
  const { key } = use(props.params)

  const [isManifestOpen, setIsManifestOpen] = useState<boolean>(false)
  const [isReleaseOpen, setIsReleaseOpen] = useState<boolean>(false)
  const [isWithdrawOpen, setIsWithdrawOpen] = useState<boolean>(false)

  const router = useRouter()

  const { hasPermissionClaim } = useAuth()
  const canUpdatePackage = hasPermissionClaim(
    'Permissions.Delivery.Update',
  )
  const canViewDeployments = hasPermissionClaim('Permissions.Delivery.View')

  const messageApi = useMessage()

  const {
    data: releasePackage,
    error,
    isLoading,
    refetch,
  } = useGetReleasePackageQuery(key)

  const { data: statusHistory, isLoading: statusHistoryLoading } =
    useGetReleasePackageStatusHistoryQuery(key)

  // Skipped without the claim: the section is not offered, so the request would only ever 403.
  const { data: deployments, isLoading: deploymentsLoading } =
    useGetDeploymentsQuery(
      { packageId: releasePackage?.id },
      { skip: !releasePackage?.id || !canViewDeployments },
    )

  useDocumentTitle(
    releasePackage ? `${releasePackage.version} - Package` : 'Release Package',
  )

  const isNotFound = (error as { status?: number })?.status === 404

  useEffect(() => {
    if (error && !isNotFound) {
      console.error(error)
      messageApi.error('Failed to load the release package.')
    }
  }, [error, isNotFound, messageApi])

  if (isNotFound) {
    notFound()
  }

  if (error) {
    return (
      <Result
        status="error"
        title="Failed to load the release package"
        subTitle="Something went wrong fetching this package."
        extra={
          <Button type="primary" onClick={() => refetch()}>
            Retry
          </Button>
        }
      />
    )
  }

  if (isLoading || !releasePackage) {
    return <ReleasePackageDetailsLoading />
  }

  const available = releasePackageActionAvailability(releasePackage)

  const canEditManifest = canUpdatePackage && available.canEditManifest
  const canRelease = canUpdatePackage && available.canRelease
  const canWithdraw = canUpdatePackage && available.canWithdraw

  const actionsMenuItems: MenuProps['items'] = (() => {
    const groups: ItemType[][] = []

    if (canEditManifest) {
      groups.push([
        {
          key: 'edit-manifest',
          label: 'Edit Manifest',
          onClick: () => setIsManifestOpen(true),
        },
      ])
    }

    // The lifecycle moves: each records something that happened to the package.
    const lifecycle: ItemType[] = []
    if (canRelease) {
      lifecycle.push({
        key: 'release',
        label: 'Mark Released',
        onClick: () => setIsReleaseOpen(true),
      })
    }
    if (canWithdraw) {
      lifecycle.push({
        key: 'withdraw',
        label: 'Withdraw',
        danger: true,
        onClick: () => setIsWithdrawOpen(true),
      })
    }
    if (lifecycle.length > 0) {
      groups.push(lifecycle)
    }

    return groups
      .filter((group) => group.length > 0)
      .flatMap((group, index) =>
        index === 0
          ? group
          : [{ type: 'divider' as const, key: `divider-${index}` }, ...group],
      )
  })()

  const sections: RecordSection[] = [
    { id: ReleasePackageSections.Manifest, label: 'Manifest' },
    ...(canViewDeployments
      ? [{ id: ReleasePackageSections.Deployments, label: 'Deployments' }]
      : []),
    { id: ReleasePackageSections.StatusHistory, label: 'Status History' },
  ]

  const renderSection = (section: string) => {
    if (section === ReleasePackageSections.Deployments) {
      return (
        <DeploymentsGrid
          deployments={deployments ?? []}
          isLoading={deploymentsLoading}
          emptyMessage="This package has not been deployed."
        />
      )
    }

    if (section === ReleasePackageSections.StatusHistory) {
      return (
        <StatusHistoryTimeline
          transitions={statusHistory}
          isLoading={statusHistoryLoading}
          emptyDescription="No status changes have been recorded for this package."
        />
      )
    }

    return (
      <ReleasePackageManifest components={releasePackage.components ?? []} />
    )
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={ReleasePackageSections.Manifest}
        record={{
          name: releasePackage.version,
          subtitle: releasePackage.name ?? 'Release Package Details',
          parent: [
            { label: 'Release Packages', href: '/product-management/release-packages' },
          ],
          recordKey: String(releasePackage.key),
          tags: (
            <StatusHistoryTag
              status={releasePackage.status}
              onOpenHistory={() =>
                router.replace(
                  `?section=${ReleasePackageSections.StatusHistory}`,
                  { scroll: false },
                )
              }
            />
          ),
          actions:
            actionsMenuItems.length > 0 ? (
              <PageActions actionItems={actionsMenuItems} />
            ) : undefined,
        }}
        facts={<ReleasePackageFacts releasePackage={releasePackage} />}
      >
        {(section) => renderSection(section)}
      </RecordLayout>

      {isManifestOpen && (
        <SetReleasePackageManifestForm
          releasePackage={releasePackage}
          onFormComplete={() => {
            setIsManifestOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsManifestOpen(false)}
        />
      )}
      {isReleaseOpen && (
        <MarkReleasePackageReleasedForm
          releasePackage={releasePackage}
          onFormComplete={() => {
            setIsReleaseOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsReleaseOpen(false)}
        />
      )}
      {isWithdrawOpen && (
        <WithdrawReleasePackageForm
          releasePackage={releasePackage}
          onFormComplete={() => {
            setIsWithdrawOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsWithdrawOpen(false)}
        />
      )}
    </>
  )
}

const ReleasePackageDetailsPageWithAuthorization = requireFeatureFlag(
  authorizePage(
    ReleasePackageDetailsPage,
    'Permission',
    'Permissions.Delivery.View',
  ),
  'product-management',
)

export default ReleasePackageDetailsPageWithAuthorization
