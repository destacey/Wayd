import { ReleaseDto, StatusCategory } from '@/src/services/wayd-api'

/**
 * Which lifecycle moves a release will accept.
 *
 * Mirrors what the aggregate enforces, so a refused action is never offered. Done and Removed are the
 * terminal buckets — a released or withdrawn release refuses a cut and a target-date move, and a
 * withdrawn one refuses being released. A released one can still be withdrawn: pulling something
 * after it shipped is the case that exists for.
 */
export interface ReleaseActionAvailability {
  canCut: boolean
  canRelease: boolean
  canWithdraw: boolean
  canMoveTargetDate: boolean
}

export const releaseActionAvailability = (
  release: ReleaseDto,
): ReleaseActionAvailability => {
  const isWithdrawn = release.status.category === StatusCategory.Removed
  const isTerminal = release.status.category === StatusCategory.Done || isWithdrawn

  return {
    canCut: !release.cutDate && !isTerminal,
    canRelease: !release.releasedDate && !isWithdrawn,
    canWithdraw: !isWithdrawn,
    canMoveTargetDate: !isTerminal,
  }
}
