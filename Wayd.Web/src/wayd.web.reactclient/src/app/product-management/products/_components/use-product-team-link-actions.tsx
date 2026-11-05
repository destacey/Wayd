'use client'

import useAuth from '@/src/components/contexts/auth'
import { ProductTeamLinkDto } from '@/src/services/wayd-api'
import { ItemType } from 'antd/es/menu/interface'
import { ReactNode, useState } from 'react'
import UnlinkProductTeamForm from './unlink-product-team-form'

export interface ProductTeamLinkActions {
  /** The menu items for one link. Empty when the viewer can do nothing to it, which renders no ⋯. */
  getActionItems: (link: ProductTeamLinkDto) => ItemType[]
  /** The open dialog, if any. Render once, beside the list. */
  dialogs: ReactNode
}

/**
 * The actions on a product team link, and the dialogs they open.
 *
 * Each acts on the product the link is on, which on a rolled-up row is a descendant of the page's product —
 * the link carries it, so nothing here assumes the page.
 */
export const useProductTeamLinkActions = (): ProductTeamLinkActions => {
  const [unlinking, setUnlinking] = useState<ProductTeamLinkDto | null>(null)
  const { hasPermissionClaim } = useAuth()

  const canUpdate = hasPermissionClaim('Permissions.Products.Update')

  const close = () => setUnlinking(null)

  const getActionItems = (link: ProductTeamLinkDto): ItemType[] =>
    canUpdate
      ? [
          {
            key: 'unlink',
            label: 'Unlink',
            danger: true,
            onClick: () => setUnlinking(link),
          },
        ]
      : []

  const dialogs = unlinking ? (
    <UnlinkProductTeamForm
      link={unlinking}
      onFormComplete={close}
      onFormCancel={close}
    />
  ) : null

  return { getActionItems, dialogs }
}

export default useProductTeamLinkActions
