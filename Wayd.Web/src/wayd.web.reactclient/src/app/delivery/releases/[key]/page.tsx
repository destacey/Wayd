'use client'

import { PageActions } from '@/src/components/common'
import { RecordLayout } from '@/src/components/common/record'
import type { RecordSection } from '@/src/components/common/record'
import { StatusHistoryTimeline } from '@/src/components/common/status-workflows'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  useGetReleaseQuery,
  useGetReleaseStatusHistoryQuery,
} from '@/src/store/features/delivery/releases-api'
import { Button, MenuProps, Result } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { notFound } from 'next/navigation'
import { use, useEffect, useState } from 'react'
import CutReleaseForm from '../_components/cut-release-form'
import { releaseActionAvailability } from '../_components/release-actions'
import EditReleaseForm from '../_components/edit-release-form'
import MarkReleaseReleasedForm from '../_components/mark-release-released-form'
import MoveReleaseTargetDateForm from '../_components/move-release-target-date-form'
import WithdrawReleaseForm from '../_components/withdraw-release-form'
import ReleaseFacts from './_components/release-facts'
import ReleaseOverview from './_components/release-overview'
import ReleaseDetailsLoading from './loading'

enum ReleaseSections {
  Overview = 'overview',
  StatusHistory = 'status-history',
}

const ReleaseDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)

  const [isEditOpen, setIsEditOpen] = useState<boolean>(false)
  const [isCutOpen, setIsCutOpen] = useState<boolean>(false)
  const [isReleaseOpen, setIsReleaseOpen] = useState<boolean>(false)
  const [isWithdrawOpen, setIsWithdrawOpen] = useState<boolean>(false)
  const [isMoveTargetDateOpen, setIsMoveTargetDateOpen] = useState<boolean>(false)

  const { hasPermissionClaim } = useAuth()
  const canUpdateRelease = hasPermissionClaim('Permissions.Releases.Update')

  const messageApi = useMessage()

  const { data: release, error, isLoading, refetch } = useGetReleaseQuery(key)

  const { data: statusHistory, isLoading: statusHistoryLoading } =
    useGetReleaseStatusHistoryQuery(key)

  useDocumentTitle(release ? `${release.version} - Release` : 'Release')

  const isNotFound = (error as { status?: number })?.status === 404

  useEffect(() => {
    if (error && !isNotFound) {
      console.error(error)
      messageApi.error('Failed to load the release.')
    }
  }, [error, isNotFound, messageApi])

  if (isNotFound) {
    notFound()
  }

  if (error) {
    return (
      <Result
        status="error"
        title="Failed to load the release"
        subTitle="Something went wrong fetching this release."
        extra={
          <Button type="primary" onClick={() => refetch()}>
            Retry
          </Button>
        }
      />
    )
  }

  if (isLoading || !release) {
    return <ReleaseDetailsLoading />
  }

  const available = releaseActionAvailability(release)

  const canCut = canUpdateRelease && available.canCut
  const canRelease = canUpdateRelease && available.canRelease
  const canWithdraw = canUpdateRelease && available.canWithdraw
  const canMoveTargetDate = canUpdateRelease && available.canMoveTargetDate

  const actionsMenuItems: MenuProps['items'] = (() => {
    const groups: ItemType[][] = []

    if (canUpdateRelease) {
      groups.push([
        { key: 'edit', label: 'Edit', onClick: () => setIsEditOpen(true) },
      ])
    }

    // The lifecycle moves, grouped apart from editing the record: each records something that
    // happened rather than correcting what was written.
    const lifecycle: ItemType[] = []
    if (canCut) {
      lifecycle.push({ key: 'cut', label: 'Cut', onClick: () => setIsCutOpen(true) })
    }
    if (canRelease) {
      lifecycle.push({
        key: 'release',
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
    { id: ReleaseSections.StatusHistory, label: 'Status History' },
  ]

  const renderSection = (section: string) => {
    if (section === ReleaseSections.StatusHistory) {
      return (
        <StatusHistoryTimeline
          transitions={statusHistory}
          isLoading={statusHistoryLoading}
          emptyDescription="No status changes have been recorded for this release."
        />
      )
    }

    return <ReleaseOverview release={release} />
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={ReleaseSections.Overview}
        record={{
          name: release.version,
          subtitle: release.name ?? 'Release Details',
          parent: [
            { label: 'Releases', href: '/delivery/releases' },
            {
              label: release.product.name,
              href: `/product-management/products/${release.product.key}`,
            },
          ],
          recordKey: String(release.key),
          actions:
            actionsMenuItems.length > 0 ? (
              <PageActions actionItems={actionsMenuItems} />
            ) : undefined,
        }}
        facts={<ReleaseFacts release={release} />}
      >
        {(section) => renderSection(section)}
      </RecordLayout>

      {isEditOpen && (
        <EditReleaseForm
          release={release}
          onFormComplete={() => {
            setIsEditOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsEditOpen(false)}
        />
      )}
      {isCutOpen && (
        <CutReleaseForm
          release={release}
          onFormComplete={() => {
            setIsCutOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsCutOpen(false)}
        />
      )}
      {isReleaseOpen && (
        <MarkReleaseReleasedForm
          release={release}
          onFormComplete={() => {
            setIsReleaseOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsReleaseOpen(false)}
        />
      )}
      {isWithdrawOpen && (
        <WithdrawReleaseForm
          release={release}
          onFormComplete={() => {
            setIsWithdrawOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsWithdrawOpen(false)}
        />
      )}
      {isMoveTargetDateOpen && (
        <MoveReleaseTargetDateForm
          release={release}
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
  authorizePage(ReleaseDetailsPage, 'Permission', 'Permissions.Releases.View'),
  'product-management',
)

export default ReleaseDetailsPageWithAuthorization
