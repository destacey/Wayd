'use client'

import PageTitle from '@/src/components/common/page-title'
import { useState } from 'react'
import { useDocumentTitle } from '@/src/hooks'
import {
  ArchiveStoryMapForm,
  CreateStoryMapForm,
  DeleteStoryMapForm,
  StoryMapsGrid,
} from './_components'
import useAuth from '@/src/components/contexts/auth'
import { Button } from 'antd'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { ControlItemSwitch } from '@/src/components/common/control-items-menu'
import { ItemType } from 'antd/es/menu/interface'
import { StoryMapListDto } from '@/src/services/wayd-api'
import { useGetStoryMapsQuery } from '@/src/store/features/planning/story-maps-api'

const StoryMapsPage = () => {
  useDocumentTitle('Story Maps')

  const [openCreateForm, setOpenCreateForm] = useState(false)
  const [includeArchived, setIncludeArchived] = useState(false)
  const [archiveStoryMap, setArchiveStoryMap] =
    useState<StoryMapListDto | null>(null)
  const [deleteStoryMap, setDeleteStoryMap] = useState<StoryMapListDto | null>(
    null,
  )

  const {
    data: storyMapsData,
    isLoading,
    refetch,
  } = useGetStoryMapsQuery(includeArchived ? true : undefined)

  const { hasPermissionClaim } = useAuth()
  const canCreate = hasPermissionClaim('Permissions.StoryMaps.Create')
  const canUpdate = hasPermissionClaim('Permissions.StoryMaps.Update')
  const canDelete = hasPermissionClaim('Permissions.StoryMaps.Delete')

  const storyMaps = storyMapsData ?? []

  const controlItems: ItemType[] = [
    {
      label: (
        <ControlItemSwitch
          label="Include Archived"
          checked={includeArchived}
          onChange={setIncludeArchived}
        />
      ),
      key: 'include-archived',
      onClick: () => setIncludeArchived((prev) => !prev),
    },
  ]

  const refresh = () => {
    refetch()
  }

  const onCreateFormClosed = (wasCreated: boolean) => {
    setOpenCreateForm(false)
    if (wasCreated) {
      refetch()
    }
  }

  const handleArchive = (storyMap: StoryMapListDto) => {
    setArchiveStoryMap(storyMap)
  }

  const handleDelete = (storyMap: StoryMapListDto) => {
    setDeleteStoryMap(storyMap)
  }

  const onArchiveFormClosed = (wasArchived: boolean) => {
    setArchiveStoryMap(null)
    if (wasArchived) {
      refetch()
    }
  }

  const onDeleteFormClosed = (wasDeleted: boolean) => {
    setDeleteStoryMap(null)
    if (wasDeleted) {
      refetch()
    }
  }

  const actions = () => (
    <>
      {canCreate && (
        <Button onClick={() => setOpenCreateForm(true)}>
          Create Story Map
        </Button>
      )}
    </>
  )

  return (
    <>
      <PageTitle title="Story Maps" actions={canCreate && actions()} />
      <StoryMapsGrid
        storyMaps={storyMaps}
        isLoading={isLoading}
        refetch={refresh}
        canUpdate={canUpdate}
        canDelete={canDelete}
        gridControlMenuItems={controlItems}
        onArchiveClicked={handleArchive}
        onDeleteClicked={handleDelete}
      />
      {openCreateForm && (
        <CreateStoryMapForm
          onFormCreate={() => onCreateFormClosed(true)}
          onFormCancel={() => onCreateFormClosed(false)}
        />
      )}
      {archiveStoryMap && (
        <ArchiveStoryMapForm
          storyMap={archiveStoryMap}
          onFormComplete={() => onArchiveFormClosed(true)}
          onFormCancel={() => onArchiveFormClosed(false)}
        />
      )}
      {deleteStoryMap && (
        <DeleteStoryMapForm
          storyMap={deleteStoryMap}
          onFormComplete={() => onDeleteFormClosed(true)}
          onFormCancel={() => onDeleteFormClosed(false)}
        />
      )}
    </>
  )
}

const StoryMapsPageWithAuthorization = requireFeatureFlag(
  authorizePage(StoryMapsPage, 'Permission', 'Permissions.StoryMaps.View'),
  'story-maps',
)

export default StoryMapsPageWithAuthorization

