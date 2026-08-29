'use client'

import useAuth from '@/src/components/contexts/auth'
import { ItemType } from 'antd/es/menu/interface'
import { ReactNode, useState } from 'react'
import ChangeExpenditureCategoryStateForm, {
  ExpenditureCategoryStateAction,
} from './change-expenditure-category-state-form'
import DeleteExpenditureCategoryForm from './delete-expenditure-category-form'
import EditExpenditureCategoryForm from './edit-expenditure-category-form'
import { ExpenditureCategoryActionTarget } from './types'

/** The dialogs a category can open. One value, not one boolean each. */
type DialogId = 'edit' | 'activate' | 'archive' | 'delete'

export interface ExpenditureCategoryActions {
  /**
   * The menu items for one category. Empty when the viewer can do nothing to
   * it, which both the row and the panel read as "render no ⋯".
   */
  getActionItems: (category: ExpenditureCategoryActionTarget) => ItemType[]
  /** The open dialog, if any. Render once, beside the list. */
  dialogs: ReactNode
}

export interface UseExpenditureCategoryActionsOptions {
  /** Called after a change that leaves the record in place. */
  onChanged: () => void
  /** Called after a delete, with the id that went — the panel closes if it
   *  was showing that record. */
  onDeleted: (id: number) => void
}

/**
 * The actions available on an expenditure category, and the dialogs they open.
 *
 * Which actions exist depends on the record's state as well as the viewer's
 * permissions: a Proposed category can be deleted or activated, an Active one
 * archived, and an Archived one neither.
 *
 * The dialogs are rendered once for the whole list rather than per row — the
 * target travels with the open dialog, so a fifty-row grid mounts one set.
 */
export const useExpenditureCategoryActions = ({
  onChanged,
  onDeleted,
}: UseExpenditureCategoryActionsOptions): ExpenditureCategoryActions => {
  const [active, setActive] = useState<{
    dialog: DialogId
    target: ExpenditureCategoryActionTarget
  } | null>(null)
  const { hasPermissionClaim } = useAuth()

  const canUpdate = hasPermissionClaim(
    'Permissions.ExpenditureCategories.Update',
  )
  const canDelete = hasPermissionClaim(
    'Permissions.ExpenditureCategories.Delete',
  )

  const open = (dialog: DialogId, target: ExpenditureCategoryActionTarget) =>
    setActive({ dialog, target })

  const close = (changed: boolean, deletedId?: number) => {
    setActive(null)
    if (!changed) return
    if (deletedId !== undefined) {
      onDeleted(deletedId)
    } else {
      onChanged()
    }
  }

  const getActionItems = (
    category: ExpenditureCategoryActionTarget,
  ): ItemType[] => {
    const state = category.state.name
    const canBeDeleted = state === 'Proposed'
    const canBeActivated = state === 'Proposed'
    const canBeArchived = state === 'Active'

    const items: ItemType[] = []

    if (canUpdate) {
      items.push({
        key: 'edit',
        label: 'Edit',
        onClick: () => open('edit', category),
      })
    }
    if (canDelete && canBeDeleted) {
      items.push({
        key: 'delete',
        label: 'Delete',
        onClick: () => open('delete', category),
      })
    }
    if (canUpdate && (canBeActivated || canBeArchived)) {
      if (items.length > 0) {
        items.push({ key: 'manage-divider', type: 'divider' })
      }
      items.push({
        key: canBeActivated ? 'activate' : 'archive',
        label: canBeActivated ? 'Activate' : 'Archive',
        onClick: () => open(canBeActivated ? 'activate' : 'archive', category),
      })
    }

    return items
  }

  const dialogs = !active ? null : (
    <>
      {active.dialog === 'edit' && (
        <EditExpenditureCategoryForm
          expenditureCategoryId={active.target.id}
          onFormComplete={() => close(true)}
          onFormCancel={() => close(false)}
        />
      )}
      {(active.dialog === 'activate' || active.dialog === 'archive') && (
        <ChangeExpenditureCategoryStateForm
          expenditureCategory={active.target}
          stateAction={
            active.dialog === 'activate'
              ? ExpenditureCategoryStateAction.Activate
              : ExpenditureCategoryStateAction.Archive
          }
          onFormComplete={() => close(true)}
          onFormCancel={() => close(false)}
        />
      )}
      {active.dialog === 'delete' && (
        <DeleteExpenditureCategoryForm
          expenditureCategory={active.target}
          onFormComplete={() => close(true, active.target.id)}
          onFormCancel={() => close(false)}
        />
      )}
    </>
  )

  return { getActionItems, dialogs }
}

export default useExpenditureCategoryActions
