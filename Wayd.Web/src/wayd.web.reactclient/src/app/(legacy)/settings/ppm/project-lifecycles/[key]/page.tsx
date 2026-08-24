'use client'

import { PageActions, PageTitle } from '@/src/components/common'
import BasicBreadcrumb from '@/src/components/common/basic-breadcrumb'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { Card, MenuProps } from 'antd'
import { use, useEffect, useState } from 'react'
import ProjectLifecycleDetailsLoading from './loading'
import { notFound, useRouter } from 'next/navigation'
import ProjectLifecycleDetails from '../_components/project-lifecycle-details'
import { useGetProjectLifecycleQuery } from '@/src/store/features/ppm/project-lifecycles-api'
import { ItemType } from 'antd/es/menu/interface'
import { useMessage } from '@/src/components/contexts/messaging'
import EditProjectLifecycleForm from '../_components/edit-project-lifecycle-form'
import DeleteProjectLifecycleForm from '../_components/delete-project-lifecycle-form'
import ChangeProjectLifecycleStateForm, {
  ProjectLifecycleStateAction,
} from '../_components/change-project-lifecycle-state-form'
import ProjectLifecycleStagesList from '../_components/project-lifecycle-stages-list'
import { useDocumentTitle } from '@/src/hooks/use-document-title'
import { isApiError } from '@/src/utils'

enum ProjectLifecycleTabs {
  Details = 'details',
  Stages = 'stages',
}

const tabs = [
  {
    key: ProjectLifecycleTabs.Details,
    tab: 'Details',
  },
  {
    key: ProjectLifecycleTabs.Stages,
    tab: 'Stages',
  },
]

enum MenuActions {
  Edit = 'Edit',
  Delete = 'Delete',
  Activate = 'Activate',
  Archive = 'Archive',
}

const ProjectLifecycleDetailsPage = (props: {
  params: Promise<{ key: number }>
}) => {
  const { key } = use(props.params)

  const [activeTab, setActiveTab] = useState(ProjectLifecycleTabs.Details)
  const [openEditForm, setOpenEditForm] = useState<boolean>(false)
  const [openActivateForm, setOpenActivateForm] = useState<boolean>(false)
  const [openArchiveForm, setOpenArchiveForm] = useState<boolean>(false)
  const [openDeleteForm, setOpenDeleteForm] = useState<boolean>(false)

  const messageApi = useMessage()

  const router = useRouter()

  const {
    data: lifecycleData,
    isLoading,
    error,
    refetch,
  } = useGetProjectLifecycleQuery(key.toString())

  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.ProjectLifecycles.Update')
  const canDelete = hasPermissionClaim('Permissions.ProjectLifecycles.Delete')

  const title = lifecycleData
    ? `${lifecycleData.name} - Project Lifecycle Details`
    : 'Project Lifecycle Details'
  useDocumentTitle(title)

  const renderTabContent = () => {
    switch (activeTab) {
      case ProjectLifecycleTabs.Details:
        return <ProjectLifecycleDetails lifecycle={lifecycleData!} />
      case ProjectLifecycleTabs.Stages:
        return (
          <ProjectLifecycleStagesList
            lifecycle={lifecycleData!}
            canManageStages={
              canUpdate && lifecycleData?.state?.name === 'Proposed'
            }
            loadData={refetch}
          />
        )
      default:
        return null
    }
  }

  const onTabChange = (tabKey: string) => {
    setActiveTab(tabKey as ProjectLifecycleTabs)
  }

  const actionsMenuItems: MenuProps['items'] = (() => {
    const currentState = lifecycleData?.state?.name
    const availableActions =
      currentState === 'Proposed'
        ? [MenuActions.Delete, MenuActions.Activate]
        : currentState === 'Active'
          ? [MenuActions.Archive]
          : []

    const items: ItemType[] = []
    if (canUpdate && currentState === 'Proposed') {
      items.push({
        key: 'edit',
        label: MenuActions.Edit,
        onClick: () => setOpenEditForm(true),
      })
    }
    if (canDelete && availableActions.includes(MenuActions.Delete)) {
      items.push({
        key: 'delete',
        label: MenuActions.Delete,
        onClick: () => setOpenDeleteForm(true),
      })
    }

    const hasStateActions =
      (canUpdate && availableActions.includes(MenuActions.Activate)) ||
      (canUpdate && availableActions.includes(MenuActions.Archive))

    if (hasStateActions && items.length > 0) {
      items.push({
        key: 'manage-divider',
        type: 'divider',
      })
    }

    if (canUpdate && availableActions.includes(MenuActions.Activate)) {
      items.push({
        key: 'activate',
        label: MenuActions.Activate,
        onClick: () => setOpenActivateForm(true),
      })
    }

    if (canUpdate && availableActions.includes(MenuActions.Archive)) {
      items.push({
        key: 'archive',
        label: MenuActions.Archive,
        onClick: () => setOpenArchiveForm(true),
      })
    }

    return items
  })()

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading project lifecycle details',
      )
      console.error(error)
    }
  }, [error, messageApi])

  const onEditFormClosed = (wasSaved: boolean) => {
    setOpenEditForm(false)
    if (wasSaved) {
      refetch()
    }
  }

  const onActivateFormClosed = (wasSaved: boolean) => {
    setOpenActivateForm(false)
    if (wasSaved) {
      refetch()
    }
  }

  const onArchiveFormClosed = (wasSaved: boolean) => {
    setOpenArchiveForm(false)
    if (wasSaved) {
      refetch()
    }
  }

  const onDeleteFormClosed = (wasDeleted: boolean) => {
    setOpenDeleteForm(false)
    if (wasDeleted) {
      router.push('/settings/ppm/project-lifecycles')
    }
  }

  if (isLoading) {
    return <ProjectLifecycleDetailsLoading />
  }

  if (!lifecycleData) {
    return notFound()
  }

  return (
    <>
      <BasicBreadcrumb
        items={[
          { title: 'Settings' },
          { title: 'PPM' },
          { title: 'Project Lifecycles', href: './' },
          { title: 'Details' },
        ]}
      />
      <PageTitle
        title={`${lifecycleData?.key} - ${lifecycleData?.name}`}
        subtitle="Project Lifecycle Details"
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

      {openEditForm && (
        <EditProjectLifecycleForm
          lifecycleId={lifecycleData?.id}
          onFormComplete={() => onEditFormClosed(true)}
          onFormCancel={() => onEditFormClosed(false)}
        />
      )}
      {openActivateForm && (
        <ChangeProjectLifecycleStateForm
          lifecycle={lifecycleData}
          stateAction={ProjectLifecycleStateAction.Activate}
          onFormComplete={() => onActivateFormClosed(true)}
          onFormCancel={() => onActivateFormClosed(false)}
        />
      )}
      {openArchiveForm && (
        <ChangeProjectLifecycleStateForm
          lifecycle={lifecycleData}
          stateAction={ProjectLifecycleStateAction.Archive}
          onFormComplete={() => onArchiveFormClosed(true)}
          onFormCancel={() => onArchiveFormClosed(false)}
        />
      )}
      {openDeleteForm && (
        <DeleteProjectLifecycleForm
          lifecycle={lifecycleData}
          onFormComplete={() => onDeleteFormClosed(true)}
          onFormCancel={() => onDeleteFormClosed(false)}
        />
      )}
    </>
  )
}

const ProjectLifecycleDetailsPageWithAuthorization = authorizePage(
  ProjectLifecycleDetailsPage,
  'Permission',
  'Permissions.ProjectLifecycles.View',
)

export default ProjectLifecycleDetailsPageWithAuthorization
