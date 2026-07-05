'use client'

import { useAppDispatch, useDocumentTitle } from '@/src/hooks'
import { BreadcrumbItem, setBreadcrumbRoute } from '@/src/store/breadcrumbs'
import {
  useGetChildWorkItemsQuery,
  useGetWorkItemQuery,
} from '@/src/store/features/work-management/workspace-api'
import { notFound, usePathname } from 'next/navigation'
import { use, useEffect, useState } from 'react'
import WorkItemDetailsLoading from './loading'
import { PageActions, PageTitle } from '@/src/components/common'
import { Card, MenuProps } from 'antd'
import { authorizePage } from '@/src/components/hoc'
import WorkItemDetails from './work-item-details'
import ExternalIconLink from '@/src/components/common/external-icon-link'
import { WorkItemsGrid } from '@/src/components/common/work'
import WorkItemDashboard from './work-item-dashboard'
import WorkItemDependencies from './work-item-dependencies'
import useAuth from '@/src/components/contexts/auth'
import { ItemType } from 'antd/es/menu/interface'
import EditWorkItemProjectForm from '../../../_components/edit-workitem-project-form'
import { WorkItemDetailsDto } from '@/src/services/wayd-api'
import { WorkTypeTier } from '@/src/components/types'

enum WorkItemTabs {
  Details = 'details',
  WorkItems = 'workItems',
  Dashboard = 'dashboard',
  Dependencies = 'dependencies',
}

const getWorkItemTabs = (workItemData: WorkItemDetailsDto) => [
  {
    key: WorkItemTabs.Details,
    tab: 'Details',
  },
  ...(workItemData?.statusCategory.name !== 'Removed'
    ? [
        {
          key: WorkItemTabs.Dashboard,
          tab: 'Dashboard',
        },
      ]
    : []),
  ...(workItemData?.type.tier.id === WorkTypeTier.Portfolio
    ? [
        {
          key: WorkItemTabs.WorkItems,
          tab: 'Work Items',
        },
      ]
    : []),
  {
    key: WorkItemTabs.Dependencies,
    tab: 'Dependencies',
  },
]

const WorkItemDetailsPage = (props: {
  params: Promise<{ key: string; workItemKey: string }>
}) => {
  const { key, workItemKey } = use(props.params)

  const upperWorkspaceKey = key.toUpperCase()
  const upperWorkItemKey = workItemKey.toUpperCase()

  useDocumentTitle(`${upperWorkItemKey} - Work Item Details`)

  const [activeTab, setActiveTab] = useState(WorkItemTabs.Details)
  const [openEditWorkItemProjectForm, setOpenEditWorkItemProjectForm] =
    useState<boolean>(false)

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

  const dispatch = useAppDispatch()
  const pathname = usePathname()

  const tabs = getWorkItemTabs(workItemData!)

  const renderTabContent = () => {
    switch (activeTab) {
      case WorkItemTabs.Details:
        return <WorkItemDetails workItem={workItemData!} />
      case WorkItemTabs.WorkItems:
        return (
          <WorkItemsGrid
            workItems={childWorkItemsQuery.data ?? []}
            isLoading={childWorkItemsQuery.isLoading}
            refetch={childWorkItemsQuery.refetch}
            hideParentColumn={true}
            persistStateKey="work-item-children"
          />
        )
      case WorkItemTabs.Dashboard:
        return <WorkItemDashboard workItem={workItemData!} />
      case WorkItemTabs.Dependencies:
        return <WorkItemDependencies workItem={workItemData!} />
      default:
        return null
    }
  }

  const onTabChange = (tabKey: string) => {
    setActiveTab(tabKey as WorkItemTabs)
  }

  const actionsMenuItems: MenuProps['items'] = (() => {
    const items: ItemType[] = []

    if (
      canManageProjectWorkItems &&
      workItemData?.type.tier.id === WorkTypeTier.Portfolio
    ) {
      items.push({
        key: 'edit-project',
        label: 'Edit Project',
        onClick: () => setOpenEditWorkItemProjectForm(true),
      })
    }

    return items
  })()

  useEffect(() => {
    const breadcrumbRoute: BreadcrumbItem[] = [
      {
        title: 'Work',
      },
      {
        title: 'Workspaces',
        href: '/work/workspaces',
      },
      {
        title: upperWorkspaceKey,
        href: `/work/workspaces/${upperWorkspaceKey}`,
      },
      {
        title: upperWorkItemKey,
        href: undefined,
      },
    ]

    dispatch(setBreadcrumbRoute({ pathname, route: breadcrumbRoute }))
  }, [dispatch, pathname, upperWorkItemKey, upperWorkspaceKey])

  useEffect(() => {
    // TODO: this isn't getting called on hook error
    error && console.error(error)
  }, [error])

  if (isLoading) {
    return <WorkItemDetailsLoading />
  }

  if (!isLoading && !workItemData) {
    return notFound()
  }
  return (
    <>
      <PageTitle
        title={
          <ExternalIconLink
            content={workItemData!.title}
            url={workItemData!.externalViewWorkItemUrl}
            tooltip="Open in external system"
          />
        }
        subtitle={`${workItemData!.type.name ?? 'Work Item'} Details`}
        actions={<PageActions actionItems={actionsMenuItems} />}
      />
      <Card
        style={{ width: '100%' }}
        tabList={tabs}
        activeTabKey={activeTab}
        onTabChange={onTabChange}
      >
        {renderTabContent()}
      </Card>
      {openEditWorkItemProjectForm && (
        <EditWorkItemProjectForm
          workItemId={workItemData!.id}
          workItemKey={workItemData!.key}
          workspaceId={workItemData!.workspace.id}
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

const WorkItemDetailsPageWithAuthorization = authorizePage(
  WorkItemDetailsPage,
  'Permission',
  'Permissions.WorkItems.View',
)

export default WorkItemDetailsPageWithAuthorization
