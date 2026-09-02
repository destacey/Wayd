import { ReleaseDto, StatusCategory } from '@/src/services/wayd-api'

/**
 * Which moves a release will accept.
 *
 * Mirrors what the aggregate enforces, so a refused action is never offered.
 *
 * The contents freeze keys on the released *date*, not on the Done category: the domain refuses an
 * amendment once a release has been announced, and once it has been withdrawn. A release sitting in a
 * Done status without a released date is not announced and can still be edited.
 *
 * Moving the target date is the one action that reads the category instead, matching the aggregate —
 * a release in a terminal status is no longer aimed at anything.
 *
 * Editing details is always offered. The domain refuses nothing there: a label or a set of notes can
 * be wrong long after the announcement, and correcting the wording says nothing about what shipped.
 */
export interface ReleaseActionAvailability {
  canEditContents: boolean
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
  const isAnnounced = !!release.releasedDate

  return {
    canEditContents: !isAnnounced && !isWithdrawn,
    canRelease: !isAnnounced && !isWithdrawn,
    canWithdraw: !isWithdrawn,
    canMoveTargetDate: !isTerminal,
    canCorrectDates: !isWithdrawn,
    canRevert: isAnnounced && !isWithdrawn,
  }
}

/**
 * The contents a release carries that have not shipped.
 *
 * Announcing is refused while any of these remain, so the form names them rather than relaying the
 * domain's generic sentence after a failed submit. Both routes are read the same way: an entry with no
 * released date has not gone anywhere yet.
 *
 * An empty release yields nothing and is not blocked — emptiness is legitimate, and only unshipped
 * contents stand in the way of an announcement.
 */
export interface OutstandingContents {
  versions: { id: string; label: string }[]
  packages: { id: string; label: string }[]
  total: number
}

export const outstandingContents = (release: ReleaseDto): OutstandingContents => {
  const versions = (release.versions ?? [])
    .filter((entry) => !entry.releasedDate)
    .map((entry) => ({
      id: entry.version.id,
      // The product qualifies the number, which says little on its own: 4.8.2 and 2026.04 are
      // indistinguishable in a list without knowing what they are versions of.
      label: entry.product?.name
        ? `${entry.product.name} ${entry.version.name}`
        : entry.version.name,
    }))

  const packages = (release.packages ?? [])
    .filter((entry) => !entry.releasedDate)
    .map((entry) => ({ id: entry.package.id, label: entry.package.name }))

  return { versions, packages, total: versions.length + packages.length }
}
