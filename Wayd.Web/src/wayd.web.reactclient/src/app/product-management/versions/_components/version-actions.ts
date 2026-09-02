import { VersionDto, StatusCategory } from '@/src/services/wayd-api'

/**
 * Which lifecycle moves a version will accept.
 *
 * Mirrors what the aggregate enforces, so a refused action is never offered. Done and Removed are the
 * terminal buckets — a released or withdrawn version refuses a cut and a target-date move, and a
 * withdrawn one refuses being released. A released one can still be withdrawn: pulling something
 * after it shipped is the case that exists for.
 *
 * Correcting dates is the one action a terminal version still accepts, since a typo outlives the
 * lifecycle. Any of the three dates can be fixed, added or — for target and cut — cleared, so the
 * only version it is refused on is a withdrawn one.
 *
 * Reverting is separate from withdrawing and answers a different question. Withdrawing says a real
 * version was pulled; reverting says it never shipped and the record was wrong. It is therefore
 * offered only where there is a released date to take back.
 */
export interface VersionActionAvailability {
  canCut: boolean
  canRelease: boolean
  canWithdraw: boolean
  canMoveTargetDate: boolean
  canCorrectDates: boolean
  canRevert: boolean
}

export const versionActionAvailability = (
  version: VersionDto,
): VersionActionAvailability => {
  const isWithdrawn = version.status.category === StatusCategory.Removed
  const isTerminal = version.status.category === StatusCategory.Done || isWithdrawn

  return {
    canCut: !version.cutDate && !isTerminal,
    canRelease: !version.releasedDate && !isWithdrawn,
    canWithdraw: !isWithdrawn,
    canMoveTargetDate: !isTerminal,
    canCorrectDates: !isWithdrawn,
    canRevert: !!version.releasedDate && !isWithdrawn,
  }
}
