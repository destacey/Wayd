'use client'

import KpiConfirmForm from '@/src/components/common/kpi/scorecard/kpi-confirm-form'
import useAuth from '@/src/components/contexts/auth'
import { KpiCategoryDto } from '@/src/services/api/wayd.schemas'
import {
  useDeleteKpiCategoryMutation,
  useSetKpiCategoryActiveMutation,
} from '@/src/store/features/common/kpis-api'
import type { ItemType } from 'antd/es/menu/interface'
import { ReactNode, useState } from 'react'
import KpiCategoryForm from './kpi-category-form'

type DialogId = 'edit' | 'deactivate' | 'activate' | 'delete'

export interface KpiCategoryActions {
  /** The menu items for one category; empty when the viewer can change none. */
  getActionItems: (category: KpiCategoryDto) => ItemType[]
  /** The open dialog, if any. Render once, beside the list. */
  dialogs: ReactNode
}

/**
 * The actions on a KPI category, and the dialogs they open. Delete is offered
 * only while no KPI has the category; one in use is deactivated instead.
 */
export const useKpiCategoryActions = (): KpiCategoryActions => {
  const [active, setActive] = useState<{
    dialog: DialogId
    target: KpiCategoryDto
  } | null>(null)
  const { hasPermissionClaim } = useAuth()
  const canManage = hasPermissionClaim('Permissions.Kpis.Administer')

  const [setCategoryActive] = useSetKpiCategoryActiveMutation()
  const [deleteCategory] = useDeleteKpiCategoryMutation()

  const open = (dialog: DialogId, target: KpiCategoryDto) =>
    setActive({ dialog, target })
  const close = () => setActive(null)

  const getActionItems = (category: KpiCategoryDto): ItemType[] => {
    if (!canManage) return []

    return [
      { key: 'edit', label: 'Edit', onClick: () => open('edit', category) },
      { key: 'state-divider', type: 'divider' },
      category.isActive
        ? {
            key: 'deactivate',
            label: 'Deactivate',
            onClick: () => open('deactivate', category),
          }
        : {
            key: 'activate',
            label: 'Activate',
            onClick: () => open('activate', category),
          },
      ...(category.kpiCount === 0
        ? [
            {
              key: 'delete',
              label: 'Delete',
              danger: true,
              onClick: () => open('delete', category),
            },
          ]
        : []),
    ]
  }

  const dialogs = !active ? null : (
    <>
      {active.dialog === 'edit' && (
        <KpiCategoryForm
          category={active.target}
          onFormComplete={close}
          onFormCancel={close}
        />
      )}
      {(active.dialog === 'deactivate' || active.dialog === 'activate') && (
        <KpiConfirmForm
          title={`${active.dialog === 'activate' ? 'Activate' : 'Deactivate'} ${active.target.name}?`}
          okText={active.dialog === 'activate' ? 'Activate' : 'Deactivate'}
          onConfirm={() =>
            setCategoryActive({
              id: active.target.id,
              isActive: active.dialog === 'activate',
            }).unwrap()
          }
          successMessage={`Category ${active.dialog === 'activate' ? 'activated' : 'deactivated'}.`}
          failureMessage="The category could not be changed."
          onFormComplete={close}
          onFormCancel={close}
        >
          {active.dialog === 'activate'
            ? 'It is offered again when a KPI is added or edited.'
            : 'It stops being offered for KPIs. The KPIs that have it keep it.'}
        </KpiConfirmForm>
      )}
      {active.dialog === 'delete' && (
        <KpiConfirmForm
          title={`Delete ${active.target.name}?`}
          okText="Delete"
          danger
          onConfirm={() => deleteCategory(active.target.id).unwrap()}
          successMessage="Category deleted."
          failureMessage="The category could not be deleted."
          onFormComplete={close}
          onFormCancel={close}
        >
          No KPI has this category, so it can be deleted.
        </KpiConfirmForm>
      )}
    </>
  )

  return { getActionItems, dialogs }
}

export default useKpiCategoryActions
