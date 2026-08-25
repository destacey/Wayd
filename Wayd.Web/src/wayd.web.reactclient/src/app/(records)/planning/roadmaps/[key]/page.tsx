'use client'

import { PageActions, WaydTooltip } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  ROADMAP_STATE,
  useGetRoadmapItemsQuery,
  useGetRoadmapQuery,
} from '@/src/store/features/planning/roadmaps-api'
import { notFound, useRouter } from 'next/navigation'
import RoadmapDetailsLoading from './loading'
import { use, useState } from 'react'
import { LockOutlined, UnlockOutlined } from '@ant-design/icons'
import { MenuProps, Space, Tag } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import {
  ChangeRoadmapStateForm,
  ConfigureRoadmapColorsForm,
  CopyRoadmapForm,
  DeleteRoadmapForm,
  EditRoadmapForm,
  RoadmapItemDrawer,
  RoadmapViewManager,
} from '@/src/app/(legacy)/planning/roadmaps/_components'
import { RoadmapStateAction } from '@/src/app/(legacy)/planning/roadmaps/_components/change-roadmap-state-form'
import CreateRoadmapActivityForm from '@/src/app/(legacy)/planning/roadmaps/_components/create-roadmap-activity-form'
import CreateRoadmapTimeboxForm from '@/src/app/(legacy)/planning/roadmaps/_components/create-roadmap-timebox-form'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import RoadmapFacts from './_components/roadmap-facts'

enum RoadmapSections {
  Plan = 'plan',
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

  const router = useRouter()

  const { user, hasPermissionClaim } = useAuth()
  const currentUserInternalEmployeeId = user?.employeeId
  const canUpdateRoadmap = hasPermissionClaim('Permissions.Roadmaps.Update')
  const canDeleteRoadmap = hasPermissionClaim('Permissions.Roadmaps.Delete')
  // Copying creates a roadmap managed by the current user, so it needs an employee link as well as
  // the permission — the API rejects an unlinked account with 403. The manager-only actions below
  // are already unreachable without a link, since isRoadmapManager requires one.
  const canCreateRoadmap =
    hasPermissionClaim('Permissions.Roadmaps.Create') &&
    !!currentUserInternalEmployeeId

  const {
    data: roadmap,
    isLoading,
    refetch: refetchRoadmap,
  } = useGetRoadmapQuery(roadmapKey.toString())

  useDocumentTitle(`${roadmap?.name ?? roadmapKey} - Roadmap Details`)

  const {
    data: roadmapItems,
    isLoading: isRoadmapItemsLoading,
    refetch: refetchRoadmapItems,
  } = useGetRoadmapItemsQuery(roadmap?.id ?? '', {
    skip: !roadmap,
  })

  const isRoadmapManager =
    !!roadmap &&
    !!currentUserInternalEmployeeId &&
    roadmap.roadmapManagers.some((rm) => rm.id === currentUserInternalEmployeeId)

  const isArchived = roadmap?.state?.id === ROADMAP_STATE.Archived

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

  const onDrawerClose = () => {
    setDrawerOpen(false)
    setSelectedItemId(null)
  }

  const openRoadmapItemDrawer = (itemId: string) => {
    setSelectedItemId(itemId)
    setDrawerOpen(true)
  }

  if (isLoading) {
    return <RoadmapDetailsLoading />
  }

  if (!roadmap) {
    return notFound()
  }

  // The managers are the panel's Relationships group; the icon says which of
  // the two visibilities is set without spending a row on it.
  const visibilityTag = (
    <WaydTooltip title={`This roadmap is ${roadmap.visibility?.name}`}>
      {roadmap.visibility?.name === 'Public' ? (
        <UnlockOutlined />
      ) : (
        <LockOutlined />
      )}
    </WaydTooltip>
  )

  // One section: the plan is the roadmap, and RoadmapViewManager already owns
  // the Gantt/Timeline/Grid switch inside it.
  const sections: RecordSection[] = [
    { id: RoadmapSections.Plan, label: 'Plan' },
  ]

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={RoadmapSections.Plan}
        record={{
          name: roadmap.name,
          recordKey: String(roadmap.key),
          subtitle: 'Roadmap Details',
          parent: { label: 'Roadmaps', href: '/planning/roadmaps' },
          tags: (
            <Space>
              {visibilityTag}
              {isArchived && <Tag>Archived</Tag>}
            </Space>
          ),
          actions: <PageActions actionItems={actionsMenuItems} />,
        }}
        facts={<RoadmapFacts roadmap={roadmap} />}
      >
        {() => (
          <RoadmapViewManager
            roadmap={roadmap}
            roadmapItems={roadmapItems ?? []}
            isRoadmapItemsLoading={isRoadmapItemsLoading}
            refreshRoadmapItems={refetchRoadmapItems}
            canUpdateRoadmap={
              canUpdateRoadmap && isRoadmapManager && !isArchived
            }
            openRoadmapItemDrawer={openRoadmapItemDrawer}
          />
        )}
      </RecordLayout>
      {openEditRoadmapForm && (
        <EditRoadmapForm
          roadmapKey={roadmapKey}
          onFormComplete={() => onEditRoadmapFormClosed(true)}
          onFormCancel={() => onEditRoadmapFormClosed(false)}
        />
      )}
      {openConfigureColorsForm && (
        <ConfigureRoadmapColorsForm
          roadmap={roadmap}
          onFormComplete={() => onConfigureColorsFormClosed(true)}
          onFormCancel={() => onConfigureColorsFormClosed(false)}
        />
      )}
      {openCopyRoadmapForm && (
        <CopyRoadmapForm
          sourceRoadmapId={roadmap.id}
          sourceRoadmapName={roadmap.name}
          onFormComplete={onCopyRoadmapFormClosed}
          onFormCancel={onCopyRoadmapFormClosed}
        />
      )}
      {openDeleteRoadmapForm && (
        <DeleteRoadmapForm
          roadmap={roadmap}
          onFormComplete={() => onDeleteFormClosed(true)}
          onFormCancel={() => onDeleteFormClosed(false)}
        />
      )}
      {openCreateActivityForm && (
        <CreateRoadmapActivityForm
          roadmapId={roadmap.id}
          onFormComplete={() => onCreateRoadmapActivityFormClosed(true)}
          onFormCancel={() => onCreateRoadmapActivityFormClosed(false)}
        />
      )}
      {openCreateTimeboxForm && (
        <CreateRoadmapTimeboxForm
          roadmapId={roadmap.id}
          onFormComplete={() => onCreateRoadmapTimeboxFormClosed(true)}
          onFormCancel={() => onCreateRoadmapTimeboxFormClosed(false)}
        />
      )}
      {openChangeStateForm && (
        <ChangeRoadmapStateForm
          roadmap={roadmap}
          stateAction={openChangeStateForm}
          onFormComplete={() => onChangeStateFormClosed(true)}
          onFormCancel={() => onChangeStateFormClosed(false)}
        />
      )}
      {selectedItemId && (
        <RoadmapItemDrawer
          roadmapId={roadmap.id}
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
