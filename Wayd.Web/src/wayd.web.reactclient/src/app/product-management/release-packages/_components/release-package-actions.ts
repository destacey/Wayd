import {
  DeliveryAction,
  ReleasePackageDto,
  StatusCategory,
} from '@/src/services/wayd-api'

/**
 * Which moves a release package will accept.
 *
 * Mirrors what the aggregate enforces, so a refused action is never offered.
 *
 * The manifest closes on release: once a package has shipped, what was in the box is a matter of
 * record, and the domain refuses an amendment. Withdrawal closes it too — a pulled package is kept
 * because deployments may reference it, not so it can be rewritten.
 *
 * Correcting dates stays open after release — a date recorded wrongly is exactly what it is for — and
 * closes only on withdrawal.
 *
 * The package's workflow has the last word on releasing and withdrawing: a restricted workflow can
 * forbid one — withdrawing a released package, say — and `workflowActions` lists those it allows
 * from the current status. Left out when unknown, nothing is withheld on its account; the server
 * still refuses a move the workflow does not allow.
 */
export interface ReleasePackageActionAvailability {
  canEditManifest: boolean
  canRelease: boolean
  canWithdraw: boolean
  canCorrectDates: boolean
}

export const releasePackageActionAvailability = (
  releasePackage: ReleasePackageDto,
  workflowActions?: readonly DeliveryAction[],
): ReleasePackageActionAvailability => {
  const isWithdrawn = releasePackage.status.category === StatusCategory.Removed
  const isReleased = !!releasePackage.releasedAt
  const allows = (action: DeliveryAction) =>
    workflowActions?.includes(action) ?? true

  return {
    canEditManifest: !isReleased && !isWithdrawn,
    canRelease:
      !isReleased &&
      !isWithdrawn &&
      (releasePackage.components?.length ?? 0) > 0 &&
      allows(DeliveryAction.MarkReleased),
    canWithdraw: !isWithdrawn && allows(DeliveryAction.Withdraw),
    canCorrectDates: !isWithdrawn,
  }
}
