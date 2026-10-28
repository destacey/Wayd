'use client'

import useAuth from '@/src/components/contexts/auth'
import { ItemType } from 'antd/es/menu/interface'
import { ReactNode, useState } from 'react'
import ChangeTagAxisActiveForm from './change-tag-axis-active-form'
import DeleteTagAxisForm from './delete-tag-axis-form'
import EditTagAxisForm from './edit-tag-axis-form'
import { axisRequestCount, TagAxisActionTarget } from './types'

/** The dialogs an axis can open. One value, not one boolean each. */
type DialogId = 'edit' | 'activate' | 'deactivate' | 'delete'

export interface TagAxisActions {
  /**
   * The menu items for one axis. Empty when the viewer can do nothing to it,
   * which both the row and the record page read as "render no ⋯".
   */
  getActionItems: (axis: TagAxisActionTarget) => ItemType[]
  /** The open dialog, if any. Render once, beside the list. */
  dialogs: ReactNode
}

export interface UseTagAxisActionsOptions {
  /** Called after a change that leaves the record in place. */
  onChanged?: () => void
  /** Called after a delete, with the id that went — the caller decides whether
   *  that means navigating away. */
  onDeleted?: (id: string) => void
}

/**
 * The actions available on a tag axis, and the dialogs they open.
 *
 * Which actions exist depends on the record as well as the viewer: a
 * platform-defined axis can only be activated or deactivated, and an axis
 * whose tags requests carry cannot be deleted, so neither is offered there.
 *
 * The dialogs are rendered once for the whole list rather than per row: the
 * target travels with the open dialog.
 */
export const useTagAxisActions = ({
  onChanged,
  onDeleted,
}: UseTagAxisActionsOptions = {}): TagAxisActions => {
  const [active, setActive] = useState<{
    dialog: DialogId
    target: TagAxisActionTarget
  } | null>(null)
  const { hasPermissionClaim } = useAuth()
  const canManage = hasPermissionClaim('Permissions.IntakeRequests.Administer')

  const open = (dialog: DialogId, target: TagAxisActionTarget) =>
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

  const getActionItems = (axis: TagAxisActionTarget): ItemType[] => {
    if (!canManage) return []

    const items: ItemType[] = []

    if (!axis.isSystem) {
      items.push({
        key: 'edit',
        label: 'Edit',
        onClick: () => open('edit', axis),
      })
      // A count of zero can still be refused — it covers only the requests the
      // viewer can see — and the dialog then shows the API's answer.
      if (axisRequestCount(axis) === 0) {
        items.push({
          key: 'delete',
          label: 'Delete',
          danger: true,
          onClick: () => open('delete', axis),
        })
      }
      items.push({ key: 'manage-divider', type: 'divider' })
    }
    items.push({
      key: axis.isActive ? 'deactivate' : 'activate',
      label: axis.isActive ? 'Deactivate' : 'Activate',
      onClick: () => open(axis.isActive ? 'deactivate' : 'activate', axis),
    })

    return items
  }

  const dialogs = !active ? null : (
    <>
      {active.dialog === 'edit' && (
        <EditTagAxisForm
          axis={active.target}
          onFormComplete={() => close(true)}
          onFormCancel={() => close(false)}
        />
      )}
      {(active.dialog === 'activate' || active.dialog === 'deactivate') && (
        <ChangeTagAxisActiveForm
          axis={active.target}
          isActive={active.dialog === 'activate'}
          onFormComplete={() => close(true)}
          onFormCancel={() => close(false)}
        />
      )}
      {active.dialog === 'delete' && (
        <DeleteTagAxisForm
          axis={active.target}
          onFormComplete={() => close(true, active.target.id)}
          onFormCancel={() => close(false)}
        />
      )}
    </>
  )

  return { getActionItems, dialogs }
}

export default useTagAxisActions
