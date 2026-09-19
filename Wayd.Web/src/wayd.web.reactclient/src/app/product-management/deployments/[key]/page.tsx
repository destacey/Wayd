'use client'

import { PageActions, WaydEmpty } from '@/src/components/common'
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
  useGetDeploymentActivitiesQuery,
  useGetDeploymentQuery,
  useGetDeploymentStatusHistoryQuery,
  useLazyGetDeploymentActivitiesQuery,
} from '@/src/store/features/product-management/deployments-api'
import {
  ACTIVITY_LOG_PAGE_SIZE,
  ActivityLogExportButton,
  ActivityLogTimeline,
  useActivityLog,
} from '@/src/components/common/activities'
import { Button, MenuProps, Result, Typography } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import { notFound, useRouter, useSearchParams } from 'next/navigation'
import { use, useEffect, useState } from 'react'
import CompleteDeploymentForm, {
  type DeploymentOutcome,
} from '../_components/complete-deployment-form'
import DeleteDeploymentForm from '../_components/delete-deployment-form'
import { deploymentActionAvailability } from '../_components/deployment-actions'
import RollBackDeploymentForm from '../_components/roll-back-deployment-form'
import DeploymentFacts from './_components/deployment-facts'
import DeploymentDetailsLoading from './loading'

const { Paragraph } = Typography

enum DeploymentSections {
  Overview = 'overview',
  StatusHistory = 'status-history',
  Activities = 'activities',
}

/**
 * A deployment, read-only apart from recording how it ended.
 *
 * There is deliberately no edit: a deployment records something that happened. Delete exists for one
 * recorded by mistake, and sits apart from the outcomes so it is not mistaken for one.
 */
const DeploymentDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)

  const [outcomeForm, setOutcomeForm] = useState<DeploymentOutcome | null>(null)
  const [isRollBackOpen, setIsRollBackOpen] = useState<boolean>(false)
  const [isDeleteOpen, setIsDeleteOpen] = useState<boolean>(false)

  const router = useRouter()

  // The active section lives in the URL (?section=), owned by RecordLayout. Read
  // here to hold the activity query back until its section is open, and because
  // sectionActions renders for whichever section that is.
  const searchParams = useSearchParams()
  const activeSection =
    searchParams.get('section') ?? DeploymentSections.Overview

  const { hasPermissionClaim } = useAuth()
  const canUpdateDeployment = hasPermissionClaim('Permissions.Delivery.Update')
  const canDeleteDeployment = hasPermissionClaim('Permissions.Delivery.Delete')

  const messageApi = useMessage()

  const {
    data: deployment,
    error,
    isLoading,
    refetch,
  } = useGetDeploymentQuery(key)

  const { data: statusHistory, isLoading: statusHistoryLoading } =
    useGetDeploymentStatusHistoryQuery(key)

  const activitiesQuery = useGetDeploymentActivitiesQuery(
    {
      idOrKey: deployment?.id ?? '',
      page: 1,
      pageSize: ACTIVITY_LOG_PAGE_SIZE,
    },
    {
      skip: !deployment?.id || activeSection !== DeploymentSections.Activities,
    },
  )
  const [fetchActivityLogPage] = useLazyGetDeploymentActivitiesQuery()

  const activityLog = useActivityLog({
    idOrKey: deployment?.id,
    query: activitiesQuery,
    fetchPage: fetchActivityLogPage,
    exportFilename: `deployment-${deployment?.key ?? key}-activity`,
  })

  useDocumentTitle(deployment ? `Deployment ${deployment.key}` : 'Deployment')

  const isNotFound = (error as { status?: number })?.status === 404

  useEffect(() => {
    if (error && !isNotFound) {
      console.error(error)
      messageApi.error('Failed to load the deployment.')
    }
  }, [error, isNotFound, messageApi])

  if (isNotFound) {
    notFound()
  }

  if (error) {
    return (
      <Result
        status="error"
        title="Failed to load the deployment"
        subTitle="Something went wrong fetching this deployment."
        extra={
          <Button type="primary" onClick={() => refetch()}>
            Retry
          </Button>
        }
      />
    )
  }

  if (isLoading || !deployment) {
    return <DeploymentDetailsLoading />
  }

  const available = deploymentActionAvailability(deployment)

  const canSucceed = canUpdateDeployment && available.canSucceed
  const canFail = canUpdateDeployment && available.canFail
  const canRollBack = canUpdateDeployment && available.canRollBack

  const actionsMenuItems: MenuProps['items'] = (() => {
    const items: ItemType[] = []

    // Only ever how it ended. There is no edit.
    if (canSucceed) {
      items.push({
        key: 'succeed',
        label: 'Record Success',
        onClick: () => setOutcomeForm('Succeeded'),
      })
    }
    if (canFail) {
      items.push({
        key: 'fail',
        label: 'Record Failure',
        danger: true,
        onClick: () => setOutcomeForm('Failed'),
      })
    }
    if (canRollBack) {
      items.push({
        key: 'roll-back',
        label: 'Roll Back',
        danger: true,
        onClick: () => setIsRollBackOpen(true),
      })
    }
    if (canDeleteDeployment) {
      if (items.length > 0) {
        items.push({ key: 'delete-divider', type: 'divider' })
      }
      items.push({
        key: 'delete',
        label: 'Delete',
        danger: true,
        onClick: () => setIsDeleteOpen(true),
      })
    }

    return items
  })()

  const sections: RecordSection[] = [
    { id: DeploymentSections.Overview, label: 'Overview' },
    { id: DeploymentSections.StatusHistory, label: 'Status History' },
    { id: DeploymentSections.Activities, label: 'Activity' },
  ]

  const renderSection = (section: string) => {
    if (section === DeploymentSections.Activities) {
      return <ActivityLogTimeline {...activityLog.timelineProps} />
    }

    if (section === DeploymentSections.StatusHistory) {
      return (
        <StatusHistoryTimeline
          transitions={statusHistory}
          isLoading={statusHistoryLoading}
          emptyDescription="No status changes have been recorded for this deployment."
        />
      )
    }

    // The facts panel carries everything else, so the reason is all that is left to show.
    return deployment.reason ? (
      <Paragraph>{deployment.reason}</Paragraph>
    ) : (
      <WaydEmpty message="No reason has been recorded for this deployment." />
    )
  }

  const deployed = deployment.version ?? deployment.package

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={DeploymentSections.Overview}
        record={{
          name: `Deployment ${deployment.key}`,
          subtitle: deployed
            ? `${deployed.name} to ${deployment.environment.name}`
            : deployment.environment.name,
          parent: [
            { label: 'Deployments', href: '/product-management/deployments' },
          ],
          recordKey: String(deployment.key),
          tags: (
            <StatusHistoryTag
              status={deployment.status}
              onOpenHistory={() =>
                router.replace(`?section=${DeploymentSections.StatusHistory}`, {
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
        facts={<DeploymentFacts deployment={deployment} />}
        sectionActions={
          activeSection === DeploymentSections.Activities ? (
            <ActivityLogExportButton activityLog={activityLog} />
          ) : undefined
        }
      >
        {(section) => renderSection(section)}
      </RecordLayout>

      {outcomeForm && (
        <CompleteDeploymentForm
          deployment={deployment}
          outcome={outcomeForm}
          onFormComplete={() => {
            setOutcomeForm(null)
            refetch()
          }}
          onFormCancel={() => setOutcomeForm(null)}
        />
      )}
      {isRollBackOpen && (
        <RollBackDeploymentForm
          deployment={deployment}
          onFormComplete={() => {
            setIsRollBackOpen(false)
            refetch()
          }}
          onFormCancel={() => setIsRollBackOpen(false)}
        />
      )}
      {isDeleteOpen && (
        <DeleteDeploymentForm
          deployment={deployment}
          onFormComplete={() => {
            setIsDeleteOpen(false)
            router.push('/product-management/deployments')
          }}
          onFormCancel={() => setIsDeleteOpen(false)}
        />
      )}
    </>
  )
}

const DeploymentDetailsPageWithAuthorization = requireFeatureFlag(
  authorizePage(
    DeploymentDetailsPage,
    'Permission',
    'Permissions.Delivery.View',
  ),
  'product-management',
)

export default DeploymentDetailsPageWithAuthorization
