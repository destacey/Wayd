'use client'

import { WaydDateRange, PageActions, PageTitle } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useAppDispatch, useDocumentTitle } from '@/src/hooks'
import {
  ROADMAP_STATE,
  useGetRoadmapItemsQuery,
  useGetRoadmapQuery,
} from '@/src/store/features/planning/roadmaps-api'
import { notFound, usePathname, useRouter } from 'next/navigation'
import RoadmapDetailsLoading from './loading'
import { use, useEffect, useState } from 'react'
import { BreadcrumbItem, setBreadcrumbRoute } from '@/src/store/breadcrumbs'
import { LockOutlined, UnlockOutlined } from '@ant-design/icons'
import { Descriptions, Divider, MenuProps, Space, Tag } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import {
  ChangeRoadmapStateForm,
  ConfigureRoadmapColorsForm,
  CopyRoadmapForm,
  DeleteRoadmapForm,
  EditRoadmapForm,
  RoadmapItemDrawer,
  RoadmapViewManager,
} from '../_components'
import { RoadmapStateAction } from '../_components/change-roadmap-state-form'
import { MarkdownRenderer } from '@/src/components/common/markdown'
import CreateRoadmapActivityForm from '../_components/create-roadmap-activity-form'
import CreateRoadmapTimeboxForm from '../_components/create-roadmap-timebox-form'

const { Item } = Descriptions

const visibilityTitle = (visibility: string, managersInfo: string) => {
  return `This roadmap is set to ${visibility}.\n\nThe roadmap managers are: ${managersInfo}`
}

const RoadmapDetailsPage = (props: { params: Promise<{ key: string }> }) => {
  const { key } = use(props.params)
  const roadmapKey = Number(key)

  const [openCreateActivityForm, setOpenCreateActivityForm] =
    useState<boolean>(false)
  const [openCreateTimeboxForm, setOpenCreateTimeboxForm] =
    useState<boolean>(false)
  const [openEditRoadmapForm, setOpenEditRoadmapForm] = useState<boolean>(false)
  const [openConfigureColorsForm, setOpenConfigureColorsForm] =
    useState<boolean>(false)
  const [openCopyRoadmapForm, setOpenCopyRoadmapForm] = useState<boolean>(false)
  const [openDeleteRoadmapForm, setOpenDeleteRoadmapForm] =
    useState<boolean>(false)
  const [openChangeStateForm, setOpenChangeStateForm] =
    useState<RoadmapStateAction | null>(null)
  const [drawerOpen, setDrawerOpen] = useState(false)
  const [selectedItemId, setSelectedItemId] = useState<string | null>(null)

  const pathname = usePathname()
  const dispatch = useAppDispatch()

  const router = useRouter()

  const { user, hasPermissionClaim } = useAuth()
  const currentUserInternalEmployeeId = user?.employeeId
  const canUpdateRoadmap = hasPermissionClaim('Permissions.Roadmaps.Update')
  const canDeleteRoadmap = hasPermissionClaim('Permissions.Roadmaps.Delete')
  const canCreateRoadmap = hasPermissionClaim('Permissions.Roadmaps.Create')

  const {
    data: roadmapData,
    isLoading,
    error,
    refetch: refetchRoadmap,
  } = useGetRoadmapQuery(roadmapKey.toString())

  useDocumentTitle(`${roadmapData?.name ?? roadmapKey} - Roadmap Details`)

  const {
    data: roadmapItems,
    isLoading: isRoadmapItemsLoading,
    refetch: refetchRoadmapItems,
  } = useGetRoadmapItemsQuery(roadmapData?.id ?? '', {
    skip: !roadmapData,
  })

  const managersInfo = !roadmapData
    ? 'Unknown'
    : roadmapData.roadmapManagers
        .slice()
        .sort((a, b) => a.name.localeCompare(b.name))
        .map((m) => m.name)
        .join(', ')

  const isRoadmapManager =
    !!roadmapData &&
    !!currentUserInternalEmployeeId &&
    roadmapData.roadmapManagers.some(
      (rm) => rm.id === currentUserInternalEmployeeId,
    )

  const isArchived = roadmapData?.state?.id === ROADMAP_STATE.Archived

  useEffect(() => {
    if (!roadmapData) return

    const breadcrumbRoute: BreadcrumbItem[] = [
      {
        title: 'Planning',
      },
      {
        href: `/planning/roadmaps`,
        title: 'Roadmaps',
      },
    ]

    breadcrumbRoute.push({
      title: 'Details',
    })

    dispatch(setBreadcrumbRoute({ route: breadcrumbRoute, pathname }))
  }, [dispatch, pathname, roadmapData])

  const actionsMenuItems: MenuProps['items'] = (() => {
    const items: ItemType[] = []

    // First section: roadmap-management actions (managers only).
    if (isRoadmapManager) {
      if (canUpdateRoadmap && !isArchived) {
        items.push({
          key: 'edit',
          label: 'Edit',
          onClick: () => setOpenEditRoadmapForm(true),
        })
      }
      if (canUpdateRoadmap && !isArchived) {
        items.push({
          key: 'archive',
          label: 'Archive',
          onClick: () => setOpenChangeStateForm(RoadmapStateAction.Archive),
        })
      }
      if (canDeleteRoadmap && !isArchived) {
        items.push({
          key: 'delete',
          label: 'Delete',
          onClick: () => setOpenDeleteRoadmapForm(true),
        })
      }
      if (canUpdateRoadmap && isArchived) {
        items.push({
          key: 'activate',
          label: 'Activate',
          onClick: () => setOpenChangeStateForm(RoadmapStateAction.Activate),
        })
      }
    }

    // Second section: configure colors (managers only) and copy (anyone who can
    // create roadmaps).
    const secondSection: ItemType[] = []
    if (isRoadmapManager && canUpdateRoadmap && !isArchived) {
      secondSection.push({
        key: 'configure-colors',
        label: 'Configure Colors',
        onClick: () => setOpenConfigureColorsForm(true),
      })
    }
    if (canCreateRoadmap) {
      secondSection.push({
        key: 'copy',
        label: 'Copy',
        onClick: () => setOpenCopyRoadmapForm(true),
      })
    }
    if (secondSection.length > 0) {
      if (items.length > 0) {
        items.push({ key: 'second-section-divider', type: 'divider' })
      }
      items.push(...secondSection)
    }

    // Third section: create actions (managers only).
    if (isRoadmapManager && canUpdateRoadmap && !isArchived) {
      items.push(
        {
          key: 'create-divider',
          type: 'divider',
        },
        {
          key: 'create-activity',
          label: 'Create Activity',
          onClick: () => setOpenCreateActivityForm(true),
        },
        {
          key: 'create-timebox',
          label: 'Create Timebox',
          onClick: () => setOpenCreateTimeboxForm(true),
        },
      )
    }

    return items
  })()

  const onEditRoadmapFormClosed = (wasSaved: boolean) => {
    setOpenEditRoadmapForm(false)
    if (wasSaved) {
      refetchRoadmap()
    }
  }

  const onConfigureColorsFormClosed = (wasSaved: boolean) => {
    setOpenConfigureColorsForm(false)
    if (wasSaved) {
      refetchRoadmap()
    }
  }

  const onCopyRoadmapFormClosed = () => {
    setOpenCopyRoadmapForm(false)
  }

  const onDeleteFormClosed = (wasDeleted: boolean) => {
    setOpenDeleteRoadmapForm(false)
    if (wasDeleted) {
      router.push('/planning/roadmaps/')
    }
  }

  const onChangeStateFormClosed = (wasChanged: boolean) => {
    setOpenChangeStateForm(null)
    if (wasChanged) {
      refetchRoadmap()
    }
  }

  const onCreateRoadmapActivityFormClosed = (wasCreated: boolean) => {
    setOpenCreateActivityForm(false)
    if (wasCreated) {
      refetchRoadmapItems()
    }
  }

  const onCreateRoadmapTimeboxFormClosed = (wasCreated: boolean) => {
    setOpenCreateTimeboxForm(false)
    if (wasCreated) {
      refetchRoadmapItems()
    }
  }

  const visibilityTag =
    roadmapData?.visibility?.name === 'Public' ? (
      <UnlockOutlined title={visibilityTitle('Public', managersInfo)} />
    ) : (
      <LockOutlined title={visibilityTitle('Private', managersInfo)} />
    )

  const headerTags = (
    <Space>
      {visibilityTag}
      {isArchived && <Tag>Archived</Tag>}
    </Space>
  )

  const showDrawer = () => {
    setDrawerOpen(true)
  }

  const onDrawerClose = () => {
    setDrawerOpen(false)
    setSelectedItemId(null)
  }

  const openRoadmapItemDrawer = (itemId: string) => {
    setSelectedItemId(itemId)
    showDrawer()
  }

  if (isLoading) {
    return <RoadmapDetailsLoading />
  }

  if (!roadmapData) {
    return notFound()
  }

  return (
    <>
      <PageTitle
        title={`${roadmapData?.key} - ${roadmapData?.name}`}
        subtitle="Roadmap Details"
        actions={<PageActions actionItems={actionsMenuItems} />}
        tags={headerTags}
      />
      {roadmapData && (
        <>
          <Descriptions>
            <Item label="Dates">
              <WaydDateRange
                dateRange={{ start: roadmapData.start, end: roadmapData.end }}
              />
            </Item>
          </Descriptions>
          <Descriptions>
            <Item>
              <MarkdownRenderer markdown={roadmapData.description} />
            </Item>
          </Descriptions>
        </>
      )}
      <Divider />
      <RoadmapViewManager
        roadmap={roadmapData}
        roadmapItems={roadmapItems ?? []}
        isRoadmapItemsLoading={isRoadmapItemsLoading}
        refreshRoadmapItems={refetchRoadmapItems}
        canUpdateRoadmap={canUpdateRoadmap && isRoadmapManager && !isArchived}
        openRoadmapItemDrawer={openRoadmapItemDrawer}
      />
      {openEditRoadmapForm && (
        <EditRoadmapForm
          roadmapKey={roadmapKey}
          onFormComplete={() => onEditRoadmapFormClosed(true)}
          onFormCancel={() => onEditRoadmapFormClosed(false)}
        />
      )}
      {openConfigureColorsForm && (
        <ConfigureRoadmapColorsForm
          roadmap={roadmapData}
          onFormComplete={() => onConfigureColorsFormClosed(true)}
          onFormCancel={() => onConfigureColorsFormClosed(false)}
        />
      )}
      {openCopyRoadmapForm && (
        <CopyRoadmapForm
          sourceRoadmapId={roadmapData?.id}
          sourceRoadmapName={roadmapData?.name}
          onFormComplete={onCopyRoadmapFormClosed}
          onFormCancel={onCopyRoadmapFormClosed}
        />
      )}
      {openDeleteRoadmapForm && (
        <DeleteRoadmapForm
          roadmap={roadmapData}
          onFormComplete={() => onDeleteFormClosed(true)}
          onFormCancel={() => onDeleteFormClosed(false)}
        />
      )}
      {openCreateActivityForm && (
        <CreateRoadmapActivityForm
          roadmapId={roadmapData?.id}
          onFormComplete={() => onCreateRoadmapActivityFormClosed(true)}
          onFormCancel={() => onCreateRoadmapActivityFormClosed(false)}
        />
      )}
      {openCreateTimeboxForm && (
        <CreateRoadmapTimeboxForm
          roadmapId={roadmapData?.id}
          onFormComplete={() => onCreateRoadmapTimeboxFormClosed(true)}
          onFormCancel={() => onCreateRoadmapTimeboxFormClosed(false)}
        />
      )}
      {openChangeStateForm && (
        <ChangeRoadmapStateForm
          roadmap={roadmapData}
          stateAction={openChangeStateForm}
          onFormComplete={() => onChangeStateFormClosed(true)}
          onFormCancel={() => onChangeStateFormClosed(false)}
        />
      )}
      {roadmapData?.id && selectedItemId && (
        <RoadmapItemDrawer
          roadmapId={roadmapData.id}
          roadmapItemId={selectedItemId}
          drawerOpen={drawerOpen}
          onDrawerClose={onDrawerClose}
          openRoadmapItemDrawer={openRoadmapItemDrawer}
          isReadOnly={isArchived}
        />
      )}
    </>
  )
}

const RoadmapDetailsPageWithAuthorization = authorizePage(
  RoadmapDetailsPage,
  'Permission',
  'Permissions.Roadmaps.View',
)

export default RoadmapDetailsPageWithAuthorization
