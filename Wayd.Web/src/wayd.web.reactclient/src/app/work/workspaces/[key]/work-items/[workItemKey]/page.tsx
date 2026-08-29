'use client'

import { useDocumentTitle } from '@/src/hooks'
import {
  useGetChildWorkItemsQuery,
  useGetWorkItemQuery,
} from '@/src/store/features/work-management/workspace-api'
import { notFound, usePathname, useRouter } from 'next/navigation'
import { Suspense, use, useEffect, useState } from 'react'
import WorkItemDetailsLoading from './loading'
import { PageActions } from '@/src/components/common'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import { MenuProps, Tag } from 'antd'
import { authorizePage } from '@/src/components/hoc'
import ExternalIconLink from '@/src/components/common/external-icon-link'
import { WorkItemsGrid } from '@/src/components/common/work'
import WorkItemDependencies from './work-item-dependencies'
import WorkItemFacts from './_components/work-item-facts'
import WorkItemOverview from './_components/work-item-overview'
import useAuth from '@/src/components/contexts/auth'
import { ItemType } from 'antd/es/menu/interface'
import EditWorkItemProjectForm from '@/src/app/work/workspaces/_components/edit-workitem-project-form'
import { WorkItemDetailsDto } from '@/src/services/wayd-api'
import { WorkTypeTier } from '@/src/components/types'

enum WorkItemSections {
  Overview = 'overview',
  WorkItems = 'work-items',
  Dependencies = 'dependencies',
}

// Overview is unconditional: it is the default section, so a record where it
// disappeared would open on whatever happened to be next.
const getSections = (workItem: WorkItemDetailsDto): RecordSection[] => [
  { id: WorkItemSections.Overview, label: 'Overview' },
  ...(workItem.type.tier.id === WorkTypeTier.Portfolio
    ? [{ id: WorkItemSections.WorkItems, label: 'Child Work Items' }]
    : []),
  { id: WorkItemSections.Dependencies, label: 'Dependencies' },
]

const WorkItemDetailsPage = (props: {
  params: Promise<{ key: string; workItemKey: string }>
}) => {
  const { key, workItemKey } = use(props.params)

  const upperWorkspaceKey = key.toUpperCase()
  const upperWorkItemKey = workItemKey.toUpperCase()

  useDocumentTitle(`${upperWorkItemKey} - Work Item Details`)

  const [openEditWorkItemProjectForm, setOpenEditWorkItemProjectForm] =
    useState<boolean>(false)

  const router = useRouter()
  const pathname = usePathname()

  const { hasPermissionClaim } = useAuth()
  const canManageProjectWorkItems = hasPermissionClaim(
    'Permissions.Projects.ManageProjectWorkItems',
  )

  const {
    data: workItemData,
    error,
    isLoading,
  } = useGetWorkItemQuery({
    idOrKey: upperWorkspaceKey,
    workItemKey: upperWorkItemKey,
  })

  const childWorkItemsQuery = useGetChildWorkItemsQuery(
    {
      idOrKey: upperWorkspaceKey,
      workItemKey: upperWorkItemKey,
    },
    { skip: !workItemData },
  )

  useEffect(() => {
    // TODO: this isn't getting called on hook error
    error && console.error(error)
  }, [error])

  if (isLoading) {
    return <WorkItemDetailsLoading />
  }

  if (!workItemData) {
    return notFound()
  }

  const actionsMenuItems: MenuProps['items'] = (() => {
    const items: ItemType[] = []

    if (
      canManageProjectWorkItems &&
      workItemData.type.tier.id === WorkTypeTier.Portfolio
    ) {
      items.push({
        key: 'edit-project',
        label: 'Edit Project',
        onClick: () => setOpenEditWorkItemProjectForm(true),
      })
    }

    return items
  })()

  // Overview's metric tiles link through to the section each one summarises.
  const goToSection = (sectionId: string) =>
    router.replace(`${pathname}?section=${sectionId}`, { scroll: false })

  const renderSection = (section: string) => {
    switch (section as WorkItemSections) {
      case WorkItemSections.WorkItems:
        return (
          <WorkItemsGrid
            workItems={childWorkItemsQuery.data ?? []}
            isLoading={childWorkItemsQuery.isLoading}
            refetch={childWorkItemsQuery.refetch}
            hideParentColumn={true}
            persistStateKey="work-item-children"
          />
        )
      case WorkItemSections.Dependencies:
        return <WorkItemDependencies workItem={workItemData} />
      default:
        return (
          <WorkItemOverview
            workItem={workItemData}
            childWorkItems={childWorkItemsQuery.data ?? []}
            childWorkItemsLoading={childWorkItemsQuery.isLoading}
            sectionIds={{
              workItems: WorkItemSections.WorkItems,
              dependencies: WorkItemSections.Dependencies,
            }}
            onNavigateToSection={goToSection}
          />
        )
    }
  }

  return (
    <>
      <RecordLayout
        sections={getSections(workItemData)}
        defaultSection={WorkItemSections.Overview}
        record={{
          name: (
            <ExternalIconLink
              content={workItemData.title}
              url={workItemData.externalViewWorkItemUrl}
              tooltip="Open in external system"
            />
          ),
          recordKey: workItemData.key,
          // The parent joins the trail so the hierarchy is walkable from the
          // bar. It is also in the facts panel, which is closed by default.
          parent: [
            {
              label: upperWorkspaceKey,
              href: `/work/workspaces/${upperWorkspaceKey}`,
            },
            ...(workItemData.parent
              ? [
                  {
                    label: workItemData.parent.key,
                    href: `/work/workspaces/${workItemData.parent.workspaceKey}/work-items/${workItemData.parent.key}`,
                  },
                ]
              : []),
          ],
          subtitle: `${workItemData.type.name ?? 'Work Item'} Details`,
          tags: <Tag>{workItemData.status}</Tag>,
          actions:
            actionsMenuItems.length > 0 ? (
              <PageActions actionItems={actionsMenuItems} />
            ) : undefined,
        }}
        facts={<WorkItemFacts workItem={workItemData} />}
      >
        {(section) => renderSection(section)}
      </RecordLayout>

      {openEditWorkItemProjectForm && (
        <EditWorkItemProjectForm
          workItemId={workItemData.id}
          workItemKey={workItemData.key}
          workspaceId={workItemData.workspace.id}
          hasParent={!!workItemData.parent}
          onFormCancel={() => setOpenEditWorkItemProjectForm(false)}
          onFormComplete={() => {
            setOpenEditWorkItemProjectForm(false)
            childWorkItemsQuery.refetch()
          }}
        />
      )}
    </>
  )
}

// useSearchParams suspends a prerendered route up to the nearest boundary. In
// development routes render on demand, so a missing one only fails the
// production build.
const WorkItemDetailsPageWithSuspense = (props: {
  params: Promise<{ key: string; workItemKey: string }>
}) => (
  <Suspense fallback={<WorkItemDetailsLoading />}>
    <WorkItemDetailsPage {...props} />
  </Suspense>
)

const WorkItemDetailsPageWithAuthorization = authorizePage(
  WorkItemDetailsPageWithSuspense,
  'Permission',
  'Permissions.WorkItems.View',
)

export default WorkItemDetailsPageWithAuthorization
