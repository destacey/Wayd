'use client'

import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import {
  IntakeRequestTagCategoryDto,
  IntakeRequestTagOptionDto,
} from '@/src/services/wayd-api'
import { Button } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { useState } from 'react'
import AddTagForm from './add-tag-form'
import ChangeTagActiveForm from './change-tag-active-form'
import DeleteTagForm from './delete-tag-form'
import EditTagForm from './edit-tag-form'

export interface TagAxisTagsListProps {
  axis: IntakeRequestTagCategoryDto
  /**
   * Whether the viewer can change the axis's tags. False on a platform-defined
   * axis whatever the viewer holds — the domain refuses those changes.
   */
  canManageTags: boolean
  loadData?: () => void
}

/** The dialogs a tag can open. One value, not one boolean each. */
type DialogId = 'edit' | 'activate' | 'deactivate' | 'delete'

/** One tag axis's tags, in the order the API returns them (alphabetical). */
const TagAxisTagsList = ({
  axis,
  canManageTags,
  loadData,
}: TagAxisTagsListProps) => {
  const [openAddTagForm, setOpenAddTagForm] = useState(false)
  const [active, setActive] = useState<{
    dialog: DialogId
    tag: IntakeRequestTagOptionDto
  } | null>(null)

  const getItems = (tag: IntakeRequestTagOptionDto): ItemType[] => {
    if (!canManageTags) return []

    const items: ItemType[] = [
      {
        key: 'edit',
        label: 'Edit',
        onClick: () => setActive({ dialog: 'edit', tag }),
      },
    ]
    // Only offered on a tag no request carries — the API refuses the rest. A
    // count can be stale or miss a private desk's requests; the dialog then
    // shows the API's answer.
    if (tag.requestCount === 0) {
      items.push({
        key: 'delete',
        label: 'Delete',
        danger: true,
        onClick: () => setActive({ dialog: 'delete', tag }),
      })
    }
    items.push(
      { key: 'active-divider', type: 'divider' },
      {
        key: tag.isActive ? 'deactivate' : 'activate',
        label: tag.isActive ? 'Deactivate' : 'Activate',
        onClick: () =>
          setActive({ dialog: tag.isActive ? 'deactivate' : 'activate', tag }),
      },
    )
    return items
  }

  const columns: ColumnDef<IntakeRequestTagOptionDto, any>[] = [
    createActionsColumn<IntakeRequestTagOptionDto>({
      unavailable: !canManageTags,
      ariaLabel: 'Tag actions',
      getItems,
    }),
    { id: 'name', accessorKey: 'name', header: 'Name', size: 200 },
    {
      id: 'description',
      accessorKey: 'description',
      header: 'Description',
      size: 400,
    },
    {
      id: 'isActive',
      accessorKey: 'isActive',
      header: 'Active',
      size: 100,
      meta: { columnType: 'yesNo' },
    },
    // What deactivating or deleting would touch, on the row itself rather
    // than only in the confirmation.
    {
      id: 'requestCount',
      accessorKey: 'requestCount',
      header: 'Requests',
      size: 110,
    },
  ]

  const actions = canManageTags ? (
    <Button type="primary" size="small" onClick={() => setOpenAddTagForm(true)}>
      Add Tag
    </Button>
  ) : null

  const closeDialog = () => setActive(null)

  return (
    <>
      <WaydGrid
        columns={columns}
        data={axis.tags ?? []}
        leftSlot={actions}
        onRefresh={loadData}
        persistStateKey="settings-intake-request-tags"
        csvFileName="intake-request-tags"
      />
      {openAddTagForm && (
        <AddTagForm
          axisId={axis.id}
          axisName={axis.name}
          onFormComplete={() => setOpenAddTagForm(false)}
          onFormCancel={() => setOpenAddTagForm(false)}
        />
      )}
      {active?.dialog === 'edit' && (
        <EditTagForm
          axisId={axis.id}
          tag={active.tag}
          onFormComplete={closeDialog}
          onFormCancel={closeDialog}
        />
      )}
      {(active?.dialog === 'activate' || active?.dialog === 'deactivate') && (
        <ChangeTagActiveForm
          axisId={axis.id}
          tag={active.tag}
          isActive={active.dialog === 'activate'}
          onFormComplete={closeDialog}
          onFormCancel={closeDialog}
        />
      )}
      {active?.dialog === 'delete' && (
        <DeleteTagForm
          axisId={axis.id}
          tag={active.tag}
          onFormComplete={closeDialog}
          onFormCancel={closeDialog}
        />
      )}
    </>
  )
}

export default TagAxisTagsList
