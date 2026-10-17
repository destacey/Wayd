import { TeamDayOffDto } from '@/src/services/wayd-api'
import { CalendarDate } from './calendar-date'

/** The percent off of a whole team day off. */
export const FULL_DAY_OFF = 100

/** Whether the team is off all of the day. */
export const isFullDayOff = (day: TeamDayOffDto): boolean =>
  day.percentOff >= FULL_DAY_OFF

/** The team's day off on `day`, if it has one. */
export const teamDayOffOn = (
  teamDaysOff: TeamDayOffDto[],
  day: CalendarDate,
): TeamDayOffDto | undefined => teamDaysOff.find((d) => d.date === day)

/**
 * A team day off as the capacity plan request takes it: the date alone for a
 * full day, followed by the percent off for part of one (`2026-10-01:50`).
 */
export const teamDayOffParam = (day: TeamDayOffDto): string =>
  isFullDayOff(day) ? day.date : `${day.date}:${day.percentOff}`
