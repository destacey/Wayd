/** How time away is entered: a whole day, half of one, or a number of hours. */
export type TimeAwayDuration = 'full' | 'half' | 'hours'

const WHOLE_DAY = 100
const HALF_DAY = 50

/** The working-day length assumed until the system setting arrives, which defaults to the same. */
export const DEFAULT_WORKING_DAY_HOURS = 8

/** Hours to two decimal places at most, without trailing zeros: 5.6, not 5.60. */
export const formatHours = (hours: number): string =>
  String(Number(hours.toFixed(2)))

/**
 * Hours to the nearest quarter hour, without trailing zeros: how time away,
 * which is entered in quarter hours, reads back from the share of the day kept.
 * A share too small for a quarter hour, which only the API can set, reads to
 * two decimal places rather than as nothing.
 */
export const formatQuarterHours = (hours: number): string => {
  const quarters = Math.round(hours * 4) / 4
  return quarters === 0 && hours > 0 ? formatHours(hours) : String(quarters)
}

/** The hours a share of a working day comes to. */
export const hoursFor = (percent: number, workingDayHours: number): number =>
  (percent * workingDayHours) / WHOLE_DAY

/**
 * The share of a working day, from 0 to 100, that `hours` is. A share is
 * stored as a whole percent, so 1.5 hours of an 8-hour day is 19%.
 */
export const percentForHours = (
  hours: number,
  workingDayHours: number,
): number =>
  Math.min(
    WHOLE_DAY,
    Math.max(0, Math.round((hours / workingDayHours) * WHOLE_DAY)),
  )

/**
 * The share of each day to record for a duration: all of it, half of it, or
 * the hours entered, never less than 1%.
 */
export function percentAwayFor(
  duration: TimeAwayDuration,
  hours: number | undefined,
  workingDayHours: number,
): number {
  if (duration === 'full') return WHOLE_DAY
  if (duration === 'half') return HALF_DAY
  return Math.max(1, percentForHours(hours ?? 0, workingDayHours))
}

/** How a recorded share of a day reads back in the form. */
export function durationOf(
  percentAway: number,
  workingDayHours: number,
): { duration: TimeAwayDuration; hours?: number } {
  if (percentAway === WHOLE_DAY) return { duration: 'full' }
  if (percentAway === HALF_DAY) return { duration: 'half' }
  return {
    duration: 'hours',
    hours: Number(formatQuarterHours(hoursFor(percentAway, workingDayHours))),
  }
}

/** A share of a day as people say it: "Full day", "Half day" or "2 h". */
export function timeAwayLabel(
  percentAway: number,
  workingDayHours: number,
): string {
  if (percentAway >= WHOLE_DAY) return 'Full day'
  if (percentAway === HALF_DAY) return 'Half day'
  return `${formatQuarterHours(hoursFor(percentAway, workingDayHours))} h`
}
