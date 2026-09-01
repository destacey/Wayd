'use client'

import useAuth from '@/src/components/contexts/auth'
import { ItemType } from 'antd/es/menu/interface'
import { ReactNode, useState } from 'react'
import ChangeProductTagCategoryActiveForm from './change-product-tag-category-active-form'
import DeleteProductTagCategoryForm from './delete-product-tag-category-form'
import EditProductTagCategoryForm from './edit-product-tag-category-form'
import { ProductTagCategoryActionTarget } from './types'

/** The dialogs a category can open. One value, not one boolean each. */
type DialogId = 'edit' | 'activate' | 'deactivate' | 'delete'

export interface ProductTagCategoryActions {
  /**
   * The menu items for one category. Empty when the viewer can do nothing to
   * it, which both the row and the record page read as "render no ⋯".
   */
  getActionItems: (category: ProductTagCategoryActionTarget) => ItemType[]
  /** The open dialog, if any. Render once, beside the list. */
  dialogs: ReactNode
}

export interface UseProductTagCategoryActionsOptions {
  /** Called after a change that leaves the record in place. */
  onChanged?: () => void
  /** Called after a delete, with the id that went — the caller decides whether
   *  that means navigating away. */
  onDeleted?: (id: string) => void
}

/**
 * The actions available on a tag axis, and the dialogs they open.
 *
 * Which actions exist depends on the record as well as the viewer's
 * permissions: a platform-seeded axis can only be activated or deactivated,
 * because the domain refuses to edit or delete one — an organization wanting
 * different axes adds its own.
 *
 * The dialogs are rendered once for the whole list rather than per row: the
 * target travels with the open dialog, so a long grid mounts one set.
 */
export const useProductTagCategoryActions = ({
  onChanged,
  onDeleted,
}: UseProductTagCategoryActionsOptions = {}): ProductTagCategoryActions => {
  const [active, setActive] = useState<{
    dialog: DialogId
    target: ProductTagCategoryActionTarget
  } | null>(null)
  const { hasPermissionClaim } = useAuth()

  const canUpdate = hasPermissionClaim('Permissions.ProductTagCategories.Update')
  const canDelete = hasPermissionClaim('Permissions.ProductTagCategories.Delete')

  const open = (dialog: DialogId, target: ProductTagCategoryActionTarget) =>
    setActive({ dialog, target })

  const close = (changed: boolean, deletedId?: string) => {
    setActive(null)
    if (!changed) return
    if (deletedId !== undefined) {
      onDeleted?.(deletedId)
    } else {
      onChanged?.()
    }
  }

  const getActionItems = (
    category: ProductTagCategoryActionTarget,
  ): ItemType[] => {
    const items: ItemType[] = []

    if (canUpdate && !category.isSystem) {
      items.push({
        key: 'edit',
        label: 'Edit',
        onClick: () => open('edit', category),
      })
    }
    if (canDelete && !category.isSystem) {
      items.push({
        key: 'delete',
        label: 'Delete',
        danger: true,
        onClick: () => open('delete', category),
      })
    }
    if (canUpdate) {
      if (items.length > 0) {
        items.push({ key: 'manage-divider', type: 'divider' })
      }
      items.push({
        key: category.isActive ? 'deactivate' : 'activate',
        label: category.isActive ? 'Deactivate' : 'Activate',
        onClick: () =>
          open(category.isActive ? 'deactivate' : 'activate', category),
      })
    }

    return items
  }

  const dialogs = !active ? null : (
    <>
      {active.dialog === 'edit' && (
        <EditProductTagCategoryForm
          category={active.target}
          onFormComplete={() => close(true)}
          onFormCancel={() => close(false)}
        />
      )}
      {(active.dialog === 'activate' || active.dialog === 'deactivate') && (
        <ChangeProductTagCategoryActiveForm
          category={active.target}
          isActive={active.dialog === 'activate'}
          onFormComplete={() => close(true)}
          onFormCancel={() => close(false)}
        />
      )}
      {active.dialog === 'delete' && (
        <DeleteProductTagCategoryForm
          category={active.target}
          onFormComplete={() => close(true, active.target.id)}
          onFormCancel={() => close(false)}
        />
      )}
    </>
  )

  return { getActionItems, dialogs }
}

export default useProductTagCategoryActions
