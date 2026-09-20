'use client'

import useAuth from '@/src/components/contexts/auth'
import { ProductDependencyDto } from '@/src/services/wayd-api'
import { ItemType } from 'antd/es/menu/interface'
import { ReactNode, useState } from 'react'
import ChangeProductDependencyTermsForm from '../../_components/change-product-dependency-terms-form'
import EditProductDependencyForm from '../../_components/edit-product-dependency-form'
import EndProductDependencyForm from '../../_components/end-product-dependency-form'
import RemoveProductDependencyForm from '../../_components/remove-product-dependency-form'

/** The dialogs a dependency can open. One value, not one boolean each. */
type DialogId = 'edit' | 'terms' | 'end' | 'remove'

export interface ProductDependencyActions {
  /**
   * The menu items for one dependency. Empty when the viewer can do nothing to it, which the row reads as
   * "render no ⋯".
   */
  getActionItems: (dependency: ProductDependencyDto) => ItemType[]
  /** The open dialog, if any. Render once, beside the list. */
  dialogs: ReactNode
}

/**
 * The actions available on a product dependency, and the dialogs they open.
 *
 * Each acts on the product that has the dependency, which on a rolled-up row is a descendant rather than the
 * page's product — the dependency carries it, so nothing here assumes the page.
 *
 * An ended dependency offers only a reworded description and removal: ending it again or changing its
 * terms would each be refused, since both would rewrite when it held.
 */
export const useProductDependencyActions = (): ProductDependencyActions => {
  const [active, setActive] = useState<{
    dialog: DialogId
    target: ProductDependencyDto
  } | null>(null)
  const { hasPermissionClaim } = useAuth()

  const canUpdate = hasPermissionClaim('Permissions.Products.Update')

  const open = (dialog: DialogId, target: ProductDependencyDto) =>
    setActive({ dialog, target })

  const close = () => setActive(null)

  const getActionItems = (dependency: ProductDependencyDto): ItemType[] => {
    if (!canUpdate) return []

    const isOpen = !dependency.endsOn

    return [
      {
        key: 'edit',
        label: 'Edit',
        onClick: () => open('edit', dependency),
      },
      ...(isOpen
        ? [
            {
              key: 'terms',
              label: 'Change Terms',
              onClick: () => open('terms', dependency),
            },
            {
              key: 'end',
              label: 'End',
              onClick: () => open('end', dependency),
            },
          ]
        : []),
      { type: 'divider', key: 'divider' },
      {
        key: 'remove',
        label: 'Remove',
        danger: true,
        onClick: () => open('remove', dependency),
      },
    ]
  }

  const formProps = active
    ? { dependency: active.target, onFormComplete: close, onFormCancel: close }
    : undefined

  const dialogs =
    !active || !formProps ? null : active.dialog === 'edit' ? (
      <EditProductDependencyForm {...formProps} />
    ) : active.dialog === 'terms' ? (
      <ChangeProductDependencyTermsForm {...formProps} />
    ) : active.dialog === 'end' ? (
      <EndProductDependencyForm {...formProps} />
    ) : (
      <RemoveProductDependencyForm {...formProps} />
    )

  return { getActionItems, dialogs }
}

export default useProductDependencyActions
