'use client'

import useAuth from '@/src/components/contexts/auth'
import { IntakeChannelDto } from '@/src/services/wayd-api'
import { ItemType } from 'antd/es/menu/interface'
import { ReactNode, useState } from 'react'
import ChangeIntakeChannelStateForm from './change-intake-channel-state-form'
import IntakeChannelForm from './intake-channel-form'

type DialogId = 'edit' | 'archive' | 'restore'

export interface IntakeChannelActions {
  /** The menu items for one channel; empty when the viewer can change none. */
  getActionItems: (channel: IntakeChannelDto) => ItemType[]
  /** The open dialog, if any. Render once, beside the list. */
  dialogs: ReactNode
}

/**
 * The actions on a channel, and the dialogs they open. There is no delete:
 * requests refer to channels, so a channel leaves use by being archived.
 */
export const useIntakeChannelActions = (): IntakeChannelActions => {
  const [active, setActive] = useState<{
    dialog: DialogId
    target: IntakeChannelDto
  } | null>(null)
  const { hasPermissionClaim } = useAuth()
  const canManage = hasPermissionClaim('Permissions.IntakeRequests.Administer')

  const open = (dialog: DialogId, target: IntakeChannelDto) =>
    setActive({ dialog, target })
  const close = () => setActive(null)

  const getActionItems = (channel: IntakeChannelDto): ItemType[] => {
    if (!canManage) return []

    return [
      { key: 'edit', label: 'Edit', onClick: () => open('edit', channel) },
      { key: 'state-divider', type: 'divider' },
      channel.isActive
        ? {
            key: 'archive',
            label: 'Archive',
            onClick: () => open('archive', channel),
          }
        : {
            key: 'restore',
            label: 'Restore',
            onClick: () => open('restore', channel),
          },
    ]
  }

  const dialogs = !active ? null : (
    <>
      {active.dialog === 'edit' && (
        <IntakeChannelForm
          channel={active.target}
          onFormComplete={close}
          onFormCancel={close}
        />
      )}
      {(active.dialog === 'archive' || active.dialog === 'restore') && (
        <ChangeIntakeChannelStateForm
          channel={active.target}
          archive={active.dialog === 'archive'}
          onFormComplete={close}
          onFormCancel={close}
        />
      )}
    </>
  )

  return { getActionItems, dialogs }
}

export default useIntakeChannelActions
