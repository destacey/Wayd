'use client'

import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { EstimationScaleDto } from '@/src/services/wayd-api'
import { useSetEstimationScaleActiveStatusMutation } from '@/src/store/features/planning/estimation-scales-api'
import { ItemType } from 'antd/es/menu/interface'
import { ReactNode, useState } from 'react'
import DeleteEstimationScaleForm from './delete-estimation-scale-form'
import EditEstimationScaleForm from './edit-estimation-scale-form'

/** The dialogs a scale can open. One value, not one boolean each. */
type DialogId = 'edit' | 'delete'

export interface EstimationScaleActions {
  /**
   * The menu items for one scale. Empty when the viewer can do nothing to it,
   * which both the row and the panel read as "render no ⋯".
   */
  getActionItems: (scale: EstimationScaleDto) => ItemType[]
  /** The open dialog, if any. Render once, beside the list. */
  dialogs: ReactNode
}

export interface UseEstimationScaleActionsOptions {
  /** Called after a change that leaves the record in place. */
  onChanged: () => void
  /** Called after a delete, with the id that went — the panel closes if it
   *  was showing that record. */
  onDeleted: (id: number) => void
}

/**
 * The actions available on an estimation scale, and the dialogs they open.
 *
 * One definition for both menus: the list's row `⋯` and the detail panel's
 * used to build their own items, which is how the record page came to offer
 * Deactivate while the row offered Delete first.
 *
 * Activation is a direct mutation rather than a dialog — it is reversible from
 * the same menu item, so a confirmation would only add a click.
 */
export const useEstimationScaleActions = ({
  onChanged,
  onDeleted,
}: UseEstimationScaleActionsOptions): EstimationScaleActions => {
  const [active, setActive] = useState<{
    dialog: DialogId
    target: EstimationScaleDto
  } | null>(null)
  const { hasPermissionClaim } = useAuth()
  const messageApi = useMessage()
  const [setActiveStatus] = useSetEstimationScaleActiveStatusMutation()

  const canUpdate = hasPermissionClaim('Permissions.EstimationScales.Update')
  const canDelete = hasPermissionClaim('Permissions.EstimationScales.Delete')

  const close = (changed: boolean, deletedId?: number) => {
    setActive(null)
    if (!changed) return
    if (deletedId !== undefined) {
      onDeleted(deletedId)
    } else {
      onChanged()
    }
  }

  const toggleActive = async (scale: EstimationScaleDto) => {
    try {
      const response = await setActiveStatus({
        id: scale.id,
        isActive: !scale.isActive,
      })
      if (response.error) {
        throw response.error
      }
      messageApi.success(
        `Estimation scale ${scale.isActive ? 'deactivated' : 'activated'} successfully.`,
      )
      onChanged()
    } catch (error) {
      messageApi.error(
        'An error occurred while updating the estimation scale status.',
      )
      console.error(error)
    }
  }

  const getActionItems = (scale: EstimationScaleDto): ItemType[] => {
    const items: ItemType[] = []

    if (canUpdate) {
      items.push({
        key: 'edit',
        label: 'Edit',
        onClick: () => setActive({ dialog: 'edit', target: scale }),
      })
      items.push({
        key: 'toggle-active',
        label: scale.isActive ? 'Deactivate' : 'Activate',
        onClick: () => toggleActive(scale),
      })
    }

    if (canDelete) {
      items.push({
        key: 'delete',
        label: 'Delete',
        danger: true,
        onClick: () => setActive({ dialog: 'delete', target: scale }),
      })
    }

    return items
  }

  const dialogs = !active ? null : (
    <>
      {active.dialog === 'edit' && (
        <EditEstimationScaleForm
          estimationScaleId={active.target.id}
          onFormComplete={() => close(true)}
          onFormCancel={() => close(false)}
        />
      )}
      {active.dialog === 'delete' && (
        <DeleteEstimationScaleForm
          estimationScale={active.target}
          onFormComplete={() => close(true, active.target.id)}
          onFormCancel={() => close(false)}
        />
      )}
    </>
  )

  return { getActionItems, dialogs }
}

export default useEstimationScaleActions
