'use client'

import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { IntakeRequestTagListItemDto } from '@/src/services/wayd-api'
import { useSearchIntakeRequestTagsQuery } from '@/src/store/features/demand-management/demand-management-api'
import { Alert } from 'antd'

import { ItemType } from 'antd/es/menu/interface'
import { useState } from 'react'
import DeleteFreeTagForm from './delete-free-tag-form'
import EditFreeTagForm from './edit-free-tag-form'
import MergeFreeTagForm from './merge-free-tag-form'

/** The dialogs a free tag can open. One value, not one boolean each. */
type DialogId = 'edit' | 'merge' | 'delete'

/**
 * The free tags agents have added — tags on no axis — with how many requests
 * carry each. A free tag nothing carries is deleted on its own, so every one
 * listed is in use somewhere.
 */
const FreeTagsList = () => {
  const {
    data: tags,
    isLoading,
    isError,
    refetch,
  } = useSearchIntakeRequestTagsQuery({
    isFree: true,
  })
  const [active, setActive] = useState<{
    dialog: DialogId
    tag: IntakeRequestTagListItemDto
  } | null>(null)

  const getItems = (tag: IntakeRequestTagListItemDto): ItemType[] => [
    {
      key: 'edit',
      label: 'Edit',
      onClick: () => setActive({ dialog: 'edit', tag }),
    },
    {
      key: 'merge',
      label: 'Merge into…',
      onClick: () => setActive({ dialog: 'merge', tag }),
    },
    { key: 'delete-divider', type: 'divider' },
    {
      key: 'delete',
      label: 'Delete',
      danger: true,
      onClick: () => setActive({ dialog: 'delete', tag }),
    },
  ]

  const columns: ColumnDef<IntakeRequestTagListItemDto, any>[] = [
    createActionsColumn<IntakeRequestTagListItemDto>({
      ariaLabel: 'Free tag actions',
      getItems,
    }),
    { id: 'name', accessorKey: 'name', header: 'Name', size: 220 },
    {
      id: 'description',
      accessorKey: 'description',
      header: 'Description',
      size: 400,
    },
    // What deleting or merging would touch, on the row itself rather than only
    // in the confirmation.
    {
      id: 'requestCount',
      accessorKey: 'requestCount',
      header: 'Requests',
      size: 110,
    },
  ]

  const closeDialog = () => setActive(null)

  return (
    <>
      {/* Without this a failed load reads as no free tags at all. */}
      {isError && (
        <Alert
          type="error"
          showIcon
          title="Could not load the free tags. Use refresh to try again."
        />
      )}
      <WaydGrid
        columns={columns}
        data={tags ?? []}
        isLoading={isLoading}
        onRefresh={async () => {
          refetch()
        }}
        persistStateKey="settings-intake-request-free-tags"
        csvFileName="intake-request-free-tags"
        getRowId={(tag: IntakeRequestTagListItemDto) => tag.id}
      />
      {active?.dialog === 'edit' && (
        <EditFreeTagForm
          tag={active.tag}
          onFormComplete={closeDialog}
          onFormCancel={closeDialog}
        />
      )}
      {active?.dialog === 'merge' && (
        <MergeFreeTagForm
          tag={active.tag}
          onFormComplete={closeDialog}
          onFormCancel={closeDialog}
        />
      )}
      {active?.dialog === 'delete' && (
        <DeleteFreeTagForm
          tag={active.tag}
          onFormComplete={closeDialog}
          onFormCancel={closeDialog}
        />
      )}
    </>
  )
}

export default FreeTagsList
