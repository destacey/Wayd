'use client'

import useAuth from '@/src/components/contexts/auth'
import type { ItemType } from 'antd/es/menu/interface'
import { ReactNode, useState } from 'react'
import ChangeTagCategoryActiveForm from './change-tag-category-active-form'
import DeleteTagCategoryForm from './delete-tag-category-form'
import EditTagCategoryForm from './edit-tag-category-form'
import { categoryRequestCount, TagCategoryActionTarget } from './types'

/** The dialogs a category can open. One value, not one boolean each. */
type DialogId = 'edit' | 'activate' | 'deactivate' | 'delete'

export interface TagCategoryActions {
  /**
   * The menu items for one category. Empty when the viewer can do nothing to it,
   * which both the row and the record page read as "render no ⋯".
   */
  getActionItems: (category: TagCategoryActionTarget) => ItemType[]
  /** The open dialog, if any. Render once, beside the list. */
  dialogs: ReactNode
}

export interface UseTagCategoryActionsOptions {
  /** Called after a change that leaves the record in place. */
  onChanged?: () => void
  /** Called after a delete, with the id that went — the caller decides whether
   *  that means navigating away. */
  onDeleted?: (id: string) => void
}

/**
 * The actions available on a tag category, and the dialogs they open.
 *
 * Which actions exist depends on the record as well as the viewer: a
 * platform-defined category can only be activated or deactivated, and a category
 * whose tags requests carry cannot be deleted, so neither is offered there.
 *
 * The dialogs are rendered once for the whole list rather than per row: the
 * target travels with the open dialog.
 */
export const useTagCategoryActions = ({
  onChanged,
  onDeleted,
}: UseTagCategoryActionsOptions = {}): TagCategoryActions => {
  const [active, setActive] = useState<{
    dialog: DialogId
    target: TagCategoryActionTarget
  } | null>(null)
  const { hasPermissionClaim } = useAuth()
  const canManage = hasPermissionClaim('Permissions.IntakeRequests.Administer')

  const open = (dialog: DialogId, target: TagCategoryActionTarget) =>
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

  const getActionItems = (category: TagCategoryActionTarget): ItemType[] => {
    if (!canManage) return []

    const items: ItemType[] = []

    if (!category.isSystem) {
      items.push({
        key: 'edit',
        label: 'Edit',
        onClick: () => open('edit', category),
      })
      // A count of zero can still be refused — it covers only the requests the
      // viewer can see — and the dialog then shows the API's answer.
      if (categoryRequestCount(category) === 0) {
        items.push({
          key: 'delete',
          label: 'Delete',
          danger: true,
          onClick: () => open('delete', category),
        })
      }
      items.push({ key: 'manage-divider', type: 'divider' })
    }
    items.push({
      key: category.isActive ? 'deactivate' : 'activate',
      label: category.isActive ? 'Deactivate' : 'Activate',
      onClick: () =>
        open(category.isActive ? 'deactivate' : 'activate', category),
    })

    return items
  }

  const dialogs = !active ? null : (
    <>
      {active.dialog === 'edit' && (
        <EditTagCategoryForm
          category={active.target}
          onFormComplete={() => close(true)}
          onFormCancel={() => close(false)}
        />
      )}
      {(active.dialog === 'activate' || active.dialog === 'deactivate') && (
        <ChangeTagCategoryActiveForm
          category={active.target}
          isActive={active.dialog === 'activate'}
          onFormComplete={() => close(true)}
          onFormCancel={() => close(false)}
        />
      )}
      {active.dialog === 'delete' && (
        <DeleteTagCategoryForm
          category={active.target}
          onFormComplete={() => close(true, active.target.id)}
          onFormCancel={() => close(false)}
        />
      )}
    </>
  )

  return { getActionItems, dialogs }
}

export default useTagCategoryActions
