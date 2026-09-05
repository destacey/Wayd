'use client'

import {
  WaydGrid,
  caseInsensitiveCompare,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import { ProductTagCategoryDto, ProductTagOptionDto } from '@/src/services/wayd-api'
import { Button } from 'antd'
import { ItemType } from 'antd/es/menu/interface'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { useMemo, useState } from 'react'
import AddProductTagForm from './add-product-tag-form'
import ChangeProductTagActiveForm from './change-product-tag-active-form'
import EditProductTagForm from './edit-product-tag-form'

export interface ProductTagsListProps {
  category: ProductTagCategoryDto
  /**
   * Whether the viewer can change the axis's tags. False for a platform-seeded
   * axis whatever the viewer holds — the domain refuses those, so offering the
   * actions would only produce a failure.
   */
  canManageTags: boolean
  loadData?: () => void
}

/** The dialogs a tag can open. One value, not one boolean each. */
type DialogId = 'edit' | 'activate' | 'deactivate'

const ProductTagsList = ({
  category,
  canManageTags,
  loadData,
}: ProductTagsListProps) => {
  const [openAddTagForm, setOpenAddTagForm] = useState(false)
  const [active, setActive] = useState<{
    dialog: DialogId
    tag: ProductTagOptionDto
  } | null>(null)

  // Alphabetical: a tag carries no position of its own, and the API returns the
  // axis's tags unordered, so presenting them is this list's business.
  const sortedTags = useMemo(
    () =>
      [...(category.tags ?? [])].sort((a, b) =>
        caseInsensitiveCompare(a.name, b.name),
      ),
    [category.tags],
  )

  const columns = useMemo<ColumnDef<ProductTagOptionDto, any>[]>(() => {
    const getItems = (tag: ProductTagOptionDto): ItemType[] => [
      {
        key: 'edit',
        label: 'Edit',
        onClick: () => setActive({ dialog: 'edit', tag }),
      },
      { key: 'active-divider', type: 'divider' },
      {
        key: tag.isActive ? 'deactivate' : 'activate',
        label: tag.isActive ? 'Deactivate' : 'Activate',
        onClick: () =>
          setActive({
            dialog: tag.isActive ? 'deactivate' : 'activate',
            tag,
          }),
      },
    ]

    return [
      createActionsColumn<ProductTagOptionDto>({
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
      // What deactivating or renaming would touch, on the row itself rather
      // than only in the confirmation.
      {
        id: 'productCount',
        accessorKey: 'productCount',
        header: 'Products',
        size: 110,
      },
    ]
  }, [canManageTags])

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
        data={sortedTags}
        leftSlot={actions}
        onRefresh={loadData}
        persistStateKey="settings-product-tags"
        csvFileName="product-tags"
      />
      {openAddTagForm && (
        <AddProductTagForm
          categoryId={category.id}
          categoryName={category.name}
          onFormComplete={() => setOpenAddTagForm(false)}
          onFormCancel={() => setOpenAddTagForm(false)}
        />
      )}
      {active?.dialog === 'edit' && (
        <EditProductTagForm
          categoryId={category.id}
          tag={active.tag}
          onFormComplete={closeDialog}
          onFormCancel={closeDialog}
        />
      )}
      {(active?.dialog === 'activate' || active?.dialog === 'deactivate') && (
        <ChangeProductTagActiveForm
          categoryId={category.id}
          tag={active.tag}
          isActive={active.dialog === 'activate'}
          onFormComplete={closeDialog}
          onFormCancel={closeDialog}
        />
      )}
    </>
  )
}

export default ProductTagsList
