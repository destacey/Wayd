'use client'

import { PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import { useGetStatusWorkflowQuery } from '@/src/store/features/common/status-workflows-api'
import { isApiError } from '@/src/utils'
import { ItemType } from 'antd/es/menu/interface'
import { notFound } from 'next/navigation'
import { use, useEffect, useState } from 'react'
import ArchiveStatusWorkflowForm from '../_components/archive-status-workflow-form'
import CloneStatusWorkflowForm from '../_components/clone-status-workflow-form'
import EditStatusWorkflowForm from '../_components/edit-status-workflow-form'
import PublishStatusWorkflowForm from '../_components/publish-status-workflow-form'
import WorkflowStatusesList from '../_components/workflow-statuses-list'
import StatusWorkflowDetailsLoading from './loading'
import { StatusWorkflowFacts } from './_components'

enum StatusWorkflowSections {
  Statuses = 'statuses',
}

/** The dialogs this record can open. One value, not one boolean each. */
type DialogId = 'edit' | 'clone' | 'publish' | 'archive'

const StatusWorkflowDetailsPage = (props: {
  params: Promise<{ key: string }>
}) => {
  const { key } = use(props.params)

  const [dialog, setDialog] = useState<DialogId | null>(null)

  const messageApi = useMessage()

  const {
    data: statusWorkflow,
    isLoading,
    error,
    refetch,
  } = useGetStatusWorkflowQuery(key)

  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.StatusWorkflows.Update')
  const canCreate = hasPermissionClaim('Permissions.StatusWorkflows.Create')

  useDocumentTitle(
    statusWorkflow
      ? `${statusWorkflow.name} - Status Workflow Details`
      : 'Status Workflow Details',
  )

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading status workflow details',
      )
      console.error(error)
    }
  }, [error, messageApi])

  // Each action needs the permission claim *and* the record's own verdict: a
  // system workflow, or one already published, is not editable no matter who
  // is asking, and only the record knows that.
  const actionsMenuItems: ItemType[] = (() => {
    if (!statusWorkflow) return []

    const items: ItemType[] = []

    if (canUpdate && statusWorkflow.canEdit) {
      items.push({ key: 'edit', label: 'Edit', onClick: () => setDialog('edit') })
    }
    if (canCreate) {
      items.push({
        key: 'clone',
        label: 'Clone',
        onClick: () => setDialog('clone'),
      })
    }

    const stateItems: ItemType[] = []
    if (canUpdate && statusWorkflow.canPublish) {
      stateItems.push({
        key: 'publish',
        label: 'Publish',
        onClick: () => setDialog('publish'),
      })
    }
    if (canUpdate && statusWorkflow.canArchive) {
      stateItems.push({
        key: 'archive',
        label: 'Archive',
        onClick: () => setDialog('archive'),
      })
    }

    if (stateItems.length > 0) {
      if (items.length > 0) {
        items.push({ key: 'state-divider', type: 'divider' })
      }
      items.push(...stateItems)
    }

    return items
  })()

  const closeDialog = (changed: boolean) => {
    setDialog(null)
    if (changed) refetch()
  }

  const sections: RecordSection[] = [
    {
      id: StatusWorkflowSections.Statuses,
      label: 'Statuses',
      count: statusWorkflow?.statuses?.length,
    },
  ]

  const renderSection = (section: string) => {
    switch (section as StatusWorkflowSections) {
      case StatusWorkflowSections.Statuses:
        return (
          <WorkflowStatusesList
            statusWorkflow={statusWorkflow!}
            loadData={refetch}
          />
        )
      default:
        return null
    }
  }

  if (isLoading) {
    return <StatusWorkflowDetailsLoading />
  }

  if (!statusWorkflow) {
    return notFound()
  }

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={StatusWorkflowSections.Statuses}
        record={{
          name: statusWorkflow.name,
          recordKey: String(statusWorkflow.key),
          parent: {
            label: 'Status Workflows',
            href: '/settings/status-workflows',
          },
          subtitle: 'Status Workflow Details',
          actions:
            actionsMenuItems.length > 0 ? (
              <PageActions actionItems={actionsMenuItems} />
            ) : undefined,
        }}
        facts={<StatusWorkflowFacts statusWorkflow={statusWorkflow} />}
      >
        {(section) => renderSection(section)}
      </RecordLayout>

      {dialog === 'edit' && (
        <EditStatusWorkflowForm
          statusWorkflow={statusWorkflow}
          onFormComplete={() => closeDialog(true)}
          onFormCancel={() => closeDialog(false)}
        />
      )}
      {dialog === 'clone' && (
        <CloneStatusWorkflowForm
          statusWorkflow={statusWorkflow}
          onFormComplete={() => closeDialog(true)}
          onFormCancel={() => closeDialog(false)}
        />
      )}
      {dialog === 'publish' && (
        <PublishStatusWorkflowForm
          statusWorkflow={statusWorkflow}
          onFormComplete={() => closeDialog(true)}
          onFormCancel={() => closeDialog(false)}
        />
      )}
      {dialog === 'archive' && (
        <ArchiveStatusWorkflowForm
          statusWorkflow={statusWorkflow}
          onFormComplete={() => closeDialog(true)}
          onFormCancel={() => closeDialog(false)}
        />
      )}
    </>
  )
}

const StatusWorkflowDetailsPageWithAuthorization = authorizePage(
  StatusWorkflowDetailsPage,
  'Permission',
  'Permissions.StatusWorkflows.View',
)

export default StatusWorkflowDetailsPageWithAuthorization
