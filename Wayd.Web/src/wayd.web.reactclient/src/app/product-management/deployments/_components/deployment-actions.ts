import { DeploymentDto, ProductStatusAlias } from '@/src/services/wayd-api'

/**
 * Which moves a deployment will accept.
 *
 * A deployment is never edited and never deleted: it records something that happened, and the only
 * thing left to record is how it ended. There is deliberately no `canEdit` here — no such endpoint
 * exists.
 *
 * Rolling back needs a success to revert. A failed deployment never reached its environment, so there
 * is nothing to take back.
 */
export interface DeploymentActionAvailability {
  canSucceed: boolean
  canFail: boolean
  canRollBack: boolean
}

export const deploymentActionAvailability = (
  deployment: DeploymentDto,
): DeploymentActionAvailability => ({
  canSucceed: !deployment.isComplete,
  canFail: !deployment.isComplete,
  canRollBack: deployment.outcome === ProductStatusAlias.Succeeded,
})
