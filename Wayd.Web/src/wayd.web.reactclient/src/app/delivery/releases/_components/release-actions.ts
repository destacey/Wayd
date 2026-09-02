import { ReleaseDto, StatusCategory } from '@/src/services/wayd-api'

/**
 * Which lifecycle moves a release will accept.
 *
 * Mirrors what the aggregate enforces, so a refused action is never offered. Done and Removed are the
 * terminal buckets — a released or withdrawn release refuses a cut and a target-date move, and a
 * withdrawn one refuses being released. A released one can still be withdrawn: pulling something
 * after it shipped is the case that exists for.
 *
 * Correcting dates is the one action a terminal release still accepts, since a typo outlives the
 * lifecycle. Any of the three dates can be fixed, added or — for target and cut — cleared, so the
 * only release it is refused on is a withdrawn one.
 *
 * Reverting is separate from withdrawing and answers a different question. Withdrawing says a real
 * release was pulled; reverting says it never shipped and the record was wrong. It is therefore
 * offered only where there is a released date to take back.
 */
export interface ReleaseActionAvailability {
  canCut: boolean
  canRelease: boolean
  canWithdraw: boolean
  canMoveTargetDate: boolean
  canCorrectDates: boolean
  canRevert: boolean
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
    canCorrectDates: !isWithdrawn,
    canRevert: !!release.releasedDate && !isWithdrawn,
  }
}
