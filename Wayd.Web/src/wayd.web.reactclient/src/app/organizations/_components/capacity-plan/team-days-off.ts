import { FULL_DAY_OFF } from '@/src/utils'
import { timeAwayLabel } from '../time-away/time-away-duration'

/**
 * How a team day off reads on its day: "Team day off", or how much of it the
 * team is off, as time away reads ("Team off half day", "Team off 2 h").
 */
export const teamDayOffLabel = (
  percentOff: number,
  workingDayHours: number,
): string =>
  percentOff >= FULL_DAY_OFF
    ? 'Team day off'
    : `Team off ${timeAwayLabel(percentOff, workingDayHours).toLowerCase()}`
