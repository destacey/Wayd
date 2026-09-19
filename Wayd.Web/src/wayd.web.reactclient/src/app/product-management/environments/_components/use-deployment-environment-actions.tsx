'use client'

import useAuth from '@/src/components/contexts/auth'
import { DeploymentEnvironmentDto } from '@/src/services/wayd-api'
import { ItemType } from 'antd/es/menu/interface'
import { ReactNode, useState } from 'react'
import DeleteEnvironmentForm from './delete-environment-form'
import DeploymentEnvironmentForm from './deployment-environment-form'
import SetEnvironmentActiveForm from './set-environment-active-form'

/** The dialogs an environment can open. One value, not one boolean each. */
type DialogId = 'edit' | 'retire' | 'reinstate' | 'delete'

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
 * Delete is offered in either state and takes every deployment into the environment with it, so
 * retiring stays the everyday way out — see {@link SetEnvironmentActiveForm}. With deployments, it
 * also needs the deployment Delete permission.
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
  const canDelete = hasPermissionClaim(
    'Permissions.DeploymentEnvironments.Delete',
  )
  const canDeleteDeployments = hasPermissionClaim('Permissions.Delivery.Delete')

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
    const items: ItemType[] = []

    if (canUpdate) {
      // A retired environment accepts neither a rename nor a reclassification, so the only thing
      // left to offer is putting it back.
      if (!environment.isActive) {
        items.push({
          key: 'reinstate',
          label: 'Reinstate',
          onClick: () => open('reinstate', environment),
        })
      } else {
        items.push(
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
        )
      }
    }

    // Deleting an environment deletes the deployments into it, which is its own grant — the API
    // refuses without it, so the action is not offered.
    if (
      canDelete &&
      (environment.deploymentCount === 0 || canDeleteDeployments)
    ) {
      if (items.length > 0) {
        items.push({ type: 'divider', key: 'delete-divider' })
      }
      items.push({
        key: 'delete',
        label: 'Delete',
        danger: true,
        onClick: () => open('delete', environment),
      })
    }

    return items
  }

  const dialogs = !active ? null : active.dialog === 'delete' ? (
    <DeleteEnvironmentForm
      environment={active.target}
      onFormComplete={() => close(true)}
      onFormCancel={() => close(false)}
    />
  ) : active.dialog === 'edit' ? (
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
