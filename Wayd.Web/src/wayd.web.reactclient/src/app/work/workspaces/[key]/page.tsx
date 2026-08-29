'use client'

import { InactiveTag } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  useGetWorkItemsQuery,
  useGetWorkspaceQuery,
} from '@/src/store/features/work-management/workspace-api'
import { Button, Spin } from 'antd'
import { notFound } from 'next/navigation'
import { Suspense, use, useEffect, useState } from 'react'
import WorkspaceDetailsLoading from './loading'
import useAuth from '@/src/components/contexts/auth'
import SetWorkspaceExternalUrlTemplatesForm from './set-workspace-external-url-templates-form'
import WorkspaceFacts from './_components/workspace-facts'
import dynamic from 'next/dynamic'

const WorkItemsGrid = dynamic(
  () => import('@/src/components/common/work/work-items-grid'),
  { ssr: false, loading: () => <Spin /> },
)

enum WorkspaceSections {
  WorkItems = 'work-items',
}

const WorkspaceDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)

  const workspaceKey = key.toUpperCase()

  useDocumentTitle('Workspace Details')

  const [
    openSetWorkspaceExternalUrlTemplatesForm,
    setOpenSetWorkspaceExternalUrlTemplatesForm,
  ] = useState<boolean>(false)

  const { hasPermissionClaim } = useAuth()
  const canUpdateWorkspace = hasPermissionClaim('Permissions.Workspaces.Update')

  const {
    data: workspaceData,
    isLoading,
    error,
    refetch,
  } = useGetWorkspaceQuery(workspaceKey)

  const workItemsQuery = useGetWorkItemsQuery(workspaceKey)

  useEffect(() => {
    workItemsQuery.error && console.error(workItemsQuery.error)
  }, [workItemsQuery.error])

  useEffect(() => {
    error && console.error(error)
  }, [error])

  if (isLoading) {
    return <WorkspaceDetailsLoading />
  }

  if (!workspaceData) {
    return notFound()
  }

  const onSetWorkspaceExternalUrlTemplatesFormClosed = (wasSaved: boolean) => {
    setOpenSetWorkspaceExternalUrlTemplatesForm(false)
    if (wasSaved) {
      refetch()
    }
  }

  // The workspace's own attributes moved into the facts panel, leaving work
  // items as the only section — so the rail is suppressed rather than holding
  // a single entry.
  const sections: RecordSection[] = [
    {
      id: WorkspaceSections.WorkItems,
      label: 'Work Items',
      count: workItemsQuery.data?.length,
    },
  ]

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={WorkspaceSections.WorkItems}
        record={{
          name: workspaceData.name,
          recordKey: workspaceData.key,
          parent: { label: 'Workspaces', href: '/work/workspaces' },
          subtitle: 'Workspace Details',
          tags: <InactiveTag isActive={workspaceData.isActive} />,
          actions: canUpdateWorkspace ? (
            <Button
              onClick={() => setOpenSetWorkspaceExternalUrlTemplatesForm(true)}
            >
              Set External URLs
            </Button>
          ) : undefined,
        }}
        facts={<WorkspaceFacts workspace={workspaceData} />}
      >
        {() => (
          <WorkItemsGrid
            workItems={workItemsQuery.data ?? []}
            isLoading={workItemsQuery.isLoading}
            refetch={workItemsQuery.refetch}
            persistStateKey="workspace-work-items"
          />
        )}
      </RecordLayout>

      {openSetWorkspaceExternalUrlTemplatesForm && (
        <SetWorkspaceExternalUrlTemplatesForm
          workspaceId={workspaceData.id}
          onFormUpdate={() => onSetWorkspaceExternalUrlTemplatesFormClosed(true)}
          onFormCancel={() =>
            onSetWorkspaceExternalUrlTemplatesFormClosed(false)
          }
        />
      )}
    </>
  )
}

// useSearchParams suspends a prerendered route up to the nearest boundary. In
// development routes render on demand, so a missing one only fails the
// production build.
const WorkspaceDetailsPageWithSuspense = (props: {
  params: Promise<{ key: string }>
}) => (
  <Suspense fallback={<WorkspaceDetailsLoading />}>
    <WorkspaceDetailsPage {...props} />
  </Suspense>
)

const WorkspaceDetailsPageWithAuthorization = authorizePage(
  WorkspaceDetailsPageWithSuspense,
  'Permission',
  'Permissions.Workspaces.View',
)

export default WorkspaceDetailsPageWithAuthorization
