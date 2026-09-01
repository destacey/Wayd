import { ReleaseDto } from '@/src/services/wayd-api'
import dayjs from 'dayjs'

/**
 * How many of these releases shipped within the last `days`.
 *
 * Counted on the released date, so a release planned or cut but not yet shipped does not — cadence
 * is about what reached people, not what was intended to.
 *
 * The window is inclusive of its first day: a release shipped exactly `days` ago counts, because a
 * reader asking for ninety days means the last ninety days rather than eighty-nine.
 */
export const countReleasedWithin = (
  releases: ReleaseDto[] | undefined,
  days: number,
): number => {
  const windowStart = dayjs().startOf('day').subtract(days, 'day')

  return (releases ?? []).filter(
    (release) =>
      release.releasedDate &&
      !dayjs(release.releasedDate).startOf('day').isBefore(windowStart),
  ).length
}
