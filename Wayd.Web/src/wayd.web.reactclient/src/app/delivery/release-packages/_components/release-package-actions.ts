import { ReleasePackageDto, StatusCategory } from '@/src/services/wayd-api'

/**
 * Which moves a release package will accept.
 *
 * Mirrors what the aggregate enforces, so a refused action is never offered.
 *
 * The manifest closes on release: once a package has shipped, what was in the box is a matter of
 * record, and the domain refuses an amendment. Withdrawal closes it too — a pulled package is kept
 * because deployments may reference it, not so it can be rewritten.
 */
export interface ReleasePackageActionAvailability {
  canEditManifest: boolean
  canRelease: boolean
  canWithdraw: boolean
}

export const releasePackageActionAvailability = (
  releasePackage: ReleasePackageDto,
): ReleasePackageActionAvailability => {
  const isWithdrawn = releasePackage.status.category === StatusCategory.Removed
  const isReleased = !!releasePackage.releasedDate

  return {
    canEditManifest: !isReleased && !isWithdrawn,
    canRelease:
      !isReleased && !isWithdrawn && (releasePackage.components?.length ?? 0) > 0,
    canWithdraw: !isWithdrawn,
  }
}
