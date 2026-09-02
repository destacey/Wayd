'use client'

import useAuth from '@/src/components/contexts/auth'
import { DeploymentEnvironmentDto } from '@/src/services/wayd-api'
import { ItemType } from 'antd/es/menu/interface'
import { ReactNode, useState } from 'react'
import DeploymentEnvironmentForm from './deployment-environment-form'
import SetEnvironmentActiveForm from './set-environment-active-form'

/** The dialogs an environment can open. One value, not one boolean each. */
type DialogId = 'edit' | 'retire' | 'reinstate'

export interface DeploymentEnvironmentActions {
  /**
   * The menu items for one environment. Empty when the viewer can do nothing to
   * it, which the row reads as "render no ⋯".
   */
  getActionItems: (environment: DeploymentEnvironmentDto) => ItemType[]
  /** The open dialog, if any. Render once, beside the list. */
  dialogs: ReactNode
}

export interface UseDeploymentEnvironmentActionsOptions {
  /** Called after a change that leaves the record in place. */
  onChanged: () => void
}

/**
 * The actions available on a deployment environment, and the dialogs they open.
 *
 * Editing is offered only on an active environment: the domain refuses to rename or reclassify a
 * retired one, so offering it would produce a refusal rather than a change. A retired environment can
 * only be reinstated.
 *
 * There is no delete, and there should not be — see {@link SetEnvironmentActiveForm}.
 *
 * The dialogs are rendered once for the whole list rather than per row — the target travels with the
 * open dialog, so a fifty-row grid mounts one set.
 */
export const useDeploymentEnvironmentActions = ({
  onChanged,
}: UseDeploymentEnvironmentActionsOptions): DeploymentEnvironmentActions => {
  const [active, setActive] = useState<{
    dialog: DialogId
    target: DeploymentEnvironmentDto
  } | null>(null)
  const { hasPermissionClaim } = useAuth()

  const canUpdate = hasPermissionClaim(
    'Permissions.DeploymentEnvironments.Update',
  )

  const open = (dialog: DialogId, target: DeploymentEnvironmentDto) =>
    setActive({ dialog, target })

  const close = (changed: boolean) => {
    setActive(null)
    if (changed) {
      onChanged()
    }
  }

  const getActionItems = (
    environment: DeploymentEnvironmentDto,
  ): ItemType[] => {
    if (!canUpdate) return []

    // A retired environment accepts neither a rename nor a reclassification, so the only thing left
    // to offer is putting it back.
    if (!environment.isActive) {
      return [
        {
          key: 'reinstate',
          label: 'Reinstate',
          onClick: () => open('reinstate', environment),
        },
      ]
    }

    return [
      {
        key: 'edit',
        label: 'Edit',
        onClick: () => open('edit', environment),
      },
      { type: 'divider', key: 'divider' },
      {
        key: 'retire',
        label: 'Retire',
        danger: true,
        onClick: () => open('retire', environment),
      },
    ]
  }

  const dialogs = !active ? null : active.dialog === 'edit' ? (
    <DeploymentEnvironmentForm
      environment={active.target}
      onFormComplete={() => close(true)}
      onFormCancel={() => close(false)}
    />
  ) : (
    <SetEnvironmentActiveForm
      environment={active.target}
      isActive={active.dialog === 'reinstate'}
      onFormComplete={() => close(true)}
      onFormCancel={() => close(false)}
    />
  )

  return { getActionItems, dialogs }
}

export default useDeploymentEnvironmentActions
