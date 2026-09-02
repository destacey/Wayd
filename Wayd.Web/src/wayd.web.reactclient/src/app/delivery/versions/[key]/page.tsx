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
  useGetVersionQuery,
  useGetVersionStatusHistoryQuery,
} from '@/src/store/features/delivery/versions-api'
import { useGetDeploymentsQuery } from '@/src/store/features/delivery/deployments-api'
import { useGetReleasePackagesQuery } from '@/src/store/features/delivery/release-packages-api'
import { DeploymentsGrid } from '../../deployments/_components'
import { ReleasePackagesGrid } from '../../release-packages/_components'
import { Button, MenuProps, Result } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { notFound, useRouter } from 'next/navigation'
import { use, useEffect, useState } from 'react'
import CorrectVersionDatesForm from '../_components/correct-version-dates-form'
import CutVersionForm from '../_components/cut-version-form'
import { releaseActionAvailability } from '../_components/version-actions'
import EditVersionForm from '../_components/edit-version-form'
import MarkVersionReleasedForm from '../_components/mark-version-released-form'
import MoveVersionTargetDateForm from '../_components/move-version-target-date-form'
import RevertVersionForm from '../_components/revert-version-form'
import WithdrawVersionForm from '../_components/withdraw-version-form'
import VersionFacts from './_components/version-facts'
import VersionOverview from './_components/version-overview'
import ReleaseDetailsLoading from './loading'

enum ReleaseSections {
  Overview = 'overview',
  Packages = 'packages',
  Deployments = 'deployments',
  StatusHistory = 'status-history',
}

const ReleaseDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)

  const [isEditOpen, setIsEditOpen] = useState<boolean>(false)
  const [isCutOpen, setIsCutOpen] = useState<boolean>(false)
  const [isCorrectDatesOpen, setIsCorrectDatesOpen] = useState<boolean>(false)
  const [isReleaseOpen, setIsReleaseOpen] = useState<boolean>(false)
  const [isWithdrawOpen, setIsWithdrawOpen] = useState<boolean>(false)
  const [isRevertOpen, setIsRevertOpen] = useState<boolean>(false)
  const [isMoveTargetDateOpen, setIsMoveTargetDateOpen] = useState<boolean>(false)

  const router = useRouter()

  const { hasPermissionClaim } = useAuth()
  const canUpdateRelease = hasPermissionClaim('Permissions.Delivery.Update')
  const canViewDeployments = hasPermissionClaim('Permissions.Delivery.View')
  const canViewPackages = hasPermissionClaim('Permissions.Delivery.View')

  const messageApi = useMessage()

  const { data: version, error, isLoading, refetch } = useGetVersionQuery(key)

  const { data: statusHistory, isLoading: statusHistoryLoading } =
    useGetVersionStatusHistoryQuery(key)

  // Skipped without the claim: the section is not offered, so the request would only ever 403.
  const { data: deployments, isLoading: deploymentsLoading } = useGetDeploymentsQuery(
    { versionId: version?.id },
    { skip: !version?.id || !canViewDeployments },
  )

  // Filtered by version rather than by product: the product-wide filter would list packages this
  // version was never part of, which reads as a wrong answer rather than a broad one.
  const { data: packages, isLoading: packagesLoading } = useGetReleasePackagesQuery(
    { containingVersionId: version?.id },
    { skip: !version?.id || !canViewPackages },
  )

  useDocumentTitle(version ? `${version.number} - Version` : 'Version')

  const isNotFound = (error as { status?: number })?.status === 404

  useEffect(() => {
    if (error && !isNotFound) {
      console.error(error)
      messageApi.error('Failed to load the version.')
    }
  }, [error, isNotFound, messageApi])

  if (isNotFound) {
    notFound()
  }

  if (error) {
    return (
      <Result
        status="error"
        title="Failed to load the version"
        subTitle="Something went wrong fetching this version."
        extra={
          <Button type="primary" onClick={() => refetch()}>
            Retry
          </Button>
        }
      />
    )
  }

  if (isLoading || !version) {
    return <ReleaseDetailsLoading />
  }

  const available = releaseActionAvailability(version)

  const canCut = canUpdateRelease && available.canCut
  const canRelease = canUpdateRelease && available.canRelease
  const canWithdraw = canUpdateRelease && available.canWithdraw
  const canMoveTargetDate = canUpdateRelease && available.canMoveTargetDate
  const canCorrectDates = canUpdateRelease && available.canCorrectDates
  const canRevert = canUpdateRelease && available.canRevert

  const actionsMenuItems: MenuProps['items'] = (() => {
    const groups: ItemType[][] = []

    // Editing the record and correcting what was written down: neither moves the version.
    const corrections: ItemType[] = []
    if (canUpdateRelease) {
      corrections.push({
        key: 'edit',
        label: 'Edit',
        onClick: () => setIsEditOpen(true),
      })
    }
    if (canCorrectDates) {
      corrections.push({
        key: 'correct-dates',
        label: 'Correct Dates',
        onClick: () => setIsCorrectDatesOpen(true),
      })
    }
    if (corrections.length > 0) {
      groups.push(corrections)
    }

    // The lifecycle moves: each records something that happened.
    //
    // Kept apart from the corrections above because they are not the same kind of act -- a
    // correction says the record was wrong, a move says the version changed.
    const lifecycle: ItemType[] = []
    if (canCut) {
      lifecycle.push({ key: 'cut', label: 'Cut', onClick: () => setIsCutOpen(true) })
    }
    if (canRelease) {
      lifecycle.push({
        key: 'version',
        label: 'Mark Released',
        onClick: () => setIsReleaseOpen(true),
      })
    }
    if (canMoveTargetDate) {
      lifecycle.push({
        key: 'move-target-date',
        label: 'Move Target Date',
        onClick: () => setIsMoveTargetDateOpen(true),
      })
    }
    if (canRevert) {
      lifecycle.push({
        key: 'revert',
        label: 'Revert Version',
        danger: true,
        onClick: () => setIsRevertOpen(true),
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
    { id: ReleaseSections.Overview, label: 'Overview' },
    ...(canViewPackages
      ? [{ id: ReleaseSections.Packages, label: 'Packages' }]
      : []),
    ...(canViewDeployments
      ? [{ id: ReleaseSections.Deployments, label: 'Deployments' }]
      : []),
    { id: ReleaseSections.StatusHistory, label: 'Status History' },
  ]

  const renderSection = (section: string) => {
    if (section === ReleaseSections.Packages) {
      return (
        <ReleasePackagesGrid
          packages={packages ?? []}
          isLoading={packagesLoading}
          emptyMessage="This version has not shipped inside a package."
        />
      )
    }

    if (section === ReleaseSections.Deployments) {
      return (
        <DeploymentsGrid
          deployments={deployments ?? []}
          isLoading={deploymentsLoading}
          emptyMessage="This version has not been deployed on its own. If it shipped inside a package, its deployments are on that package."
        />
      )
    }

    if (section === ReleaseSections.StatusHistory) {
      return (
        <StatusHistoryTimeline
          transitions={statusHistory}
          isLoading={statusHistoryLoading}
          emptyDescription="No status changes have been recorded for this version."
        />
      )
    }

    return <VersionOverview version={version} />
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={ReleaseSections.Overview}
        record={{
          name: version.number,
          subtitle: version.name ?? 'Version Details',
          parent: [
            { label: 'Versions', href: '/delivery/versions' },
            {
              label: version.product.name,
              href: `/product-management/products/${version.product.key}`,
            },
          ],
          recordKey: String(version.key),
          tags: (
            <StatusHistoryTag
              status={version.status}
              onOpenHistory={() =>
                router.replace(`?section=${ReleaseSections.StatusHistory}`, {
                  scroll: false,
                })
              }
            />
          ),
          actions:
            actionsMenuItems.length > 0 ? (
              <PageActions actionItems={actionsMenuItems} />
            ) : undefined,
        }}
        facts={<VersionFacts version={version} />}
      >
        {(section) => renderSection(section)}
      </RecordLayout>

      {isEditOpen && (
        <EditVersionForm
          version={version}
          onFormComplete={() => {
            setIsEditOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsEditOpen(false)}
        />
      )}
      {isCorrectDatesOpen && (
        <CorrectVersionDatesForm
          version={version}
          onFormComplete={() => {
            setIsCorrectDatesOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsCorrectDatesOpen(false)}
        />
      )}
      {isCutOpen && (
        <CutVersionForm
          version={version}
          onFormComplete={() => {
            setIsCutOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsCutOpen(false)}
        />
      )}
      {isReleaseOpen && (
        <MarkVersionReleasedForm
          version={version}
          onFormComplete={() => {
            setIsReleaseOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsReleaseOpen(false)}
        />
      )}
      {isWithdrawOpen && (
        <WithdrawVersionForm
          version={version}
          onFormComplete={() => {
            setIsWithdrawOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsWithdrawOpen(false)}
        />
      )}
      {isRevertOpen && (
        <RevertVersionForm
          version={version}
          onFormComplete={() => {
            setIsRevertOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsRevertOpen(false)}
        />
      )}
      {isMoveTargetDateOpen && (
        <MoveVersionTargetDateForm
          version={version}
          onFormComplete={() => {
            setIsMoveTargetDateOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsMoveTargetDateOpen(false)}
        />
      )}
    </>
  )
}

const ReleaseDetailsPageWithAuthorization = requireFeatureFlag(
  authorizePage(ReleaseDetailsPage, 'Permission', 'Permissions.Delivery.View'),
  'product-management',
)

export default ReleaseDetailsPageWithAuthorization
