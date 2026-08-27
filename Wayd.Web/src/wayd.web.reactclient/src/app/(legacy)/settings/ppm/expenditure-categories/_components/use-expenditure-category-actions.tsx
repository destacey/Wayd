'use client'

import { PageActions } from '@/src/components/common'
import useAuth from '@/src/components/contexts/auth'
import { ExpenditureCategoryDetailsDto } from '@/src/services/wayd-api'
import { ItemType } from 'antd/es/menu/interface'
import { ReactNode, useState } from 'react'
import ChangeExpenditureCategoryStateForm, {
  ExpenditureCategoryStateAction,
} from './change-expenditure-category-state-form'
import DeleteExpenditureCategoryForm from './delete-expenditure-category-form'
import EditExpenditureCategoryForm from './edit-expenditure-category-form'

/** The dialogs this record can open. One value, not one boolean each. */
type DialogId = 'edit' | 'activate' | 'archive' | 'delete'

export interface ExpenditureCategoryActions {
  /** The actions menu, or null when the viewer can do nothing. */
  actions: ReactNode
  /** The open dialog, if any. Render alongside the panel. */
  dialogs: ReactNode
}

export interface UseExpenditureCategoryActionsOptions {
  expenditureCategory: ExpenditureCategoryDetailsDto | undefined
  /** Called after a change that leaves the record in place. */
  onChanged: () => void
  /** Called after a delete — the record is gone, so the panel must close. */
  onDeleted: () => void
}

/**
 * The actions available on one expenditure category, and the dialogs they
 * open.
 *
 * Which actions exist depends on the record's state as well as the viewer's
 * permissions: a Proposed category can be deleted or activated, an Active one
 * archived, and an Archived one neither.
 */
export const useExpenditureCategoryActions = ({
  expenditureCategory,
  onChanged,
  onDeleted,
}: UseExpenditureCategoryActionsOptions): ExpenditureCategoryActions => {
  const [dialog, setDialog] = useState<DialogId | null>(null)
  const { hasPermissionClaim } = useAuth()

  const canUpdate = hasPermissionClaim(
    'Permissions.ExpenditureCategories.Update',
  )
  const canDelete = hasPermissionClaim(
    'Permissions.ExpenditureCategories.Delete',
  )

  const close = (changed: boolean, wasDelete = false) => {
    setDialog(null)
    if (!changed) return
    if (wasDelete) {
      onDeleted()
    } else {
      onChanged()
    }
  }

  const state = expenditureCategory?.state.name
  const canBeDeleted = state === 'Proposed'
  const canBeActivated = state === 'Proposed'
  const canBeArchived = state === 'Active'

  const items: ItemType[] = []

  if (expenditureCategory) {
    if (canUpdate) {
      items.push({
        key: 'edit',
        label: 'Edit',
        onClick: () => setDialog('edit'),
      })
    }
    if (canDelete && canBeDeleted) {
      items.push({
        key: 'delete',
        label: 'Delete',
        onClick: () => setDialog('delete'),
      })
    }
    if (canUpdate && (canBeActivated || canBeArchived)) {
      if (items.length > 0) {
        items.push({ key: 'manage-divider', type: 'divider' })
      }
      items.push({
        key: canBeActivated ? 'activate' : 'archive',
        label: canBeActivated ? 'Activate' : 'Archive',
        onClick: () => setDialog(canBeActivated ? 'activate' : 'archive'),
      })
    }
  }

  // Null rather than an empty PageActions: the panel draws a bordered actions
  // strip whenever this is truthy, so a read-only viewer would get an empty one.
  const actions = items.length > 0 ? <PageActions actionItems={items} /> : null

  const dialogs = !expenditureCategory ? null : (
    <>
      {dialog === 'edit' && (
        <EditExpenditureCategoryForm
          expenditureCategoryId={expenditureCategory.id}
          onFormComplete={() => close(true)}
          onFormCancel={() => close(false)}
        />
      )}
      {(dialog === 'activate' || dialog === 'archive') && (
        <ChangeExpenditureCategoryStateForm
          expenditureCategory={expenditureCategory}
          stateAction={
            dialog === 'activate'
              ? ExpenditureCategoryStateAction.Activate
              : ExpenditureCategoryStateAction.Archive
          }
          onFormComplete={() => close(true)}
          onFormCancel={() => close(false)}
        />
      )}
      {dialog === 'delete' && (
        <DeleteExpenditureCategoryForm
          expenditureCategory={expenditureCategory}
          onFormComplete={() => close(true, true)}
          onFormCancel={() => close(false)}
        />
      )}
    </>
  )

  return { actions, dialogs }
}

export default useExpenditureCategoryActions
