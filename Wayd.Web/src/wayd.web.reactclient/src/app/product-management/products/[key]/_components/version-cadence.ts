import { VersionDto } from '@/src/services/wayd-api'
import dayjs from 'dayjs'

/**
 * How many of these versions shipped within the last `days`.
 *
 * Counted on the released date, so a version planned or cut but not yet shipped does not — cadence
 * is about what reached people, not what was intended to.
 *
 * The window is inclusive of its first day: a version shipped exactly `days` ago counts, because a
 * reader asking for ninety days means the last ninety days rather than eighty-nine.
 */
export const countReleasedWithin = (
  versions: VersionDto[] | undefined,
  days: number,
): number => {
  const windowStart = dayjs().startOf('day').subtract(days, 'day')

  return (versions ?? []).filter(
    (version) =>
      version.releasedDate &&
      !dayjs(version.releasedDate).startOf('day').isBefore(windowStart),
  ).length
}
