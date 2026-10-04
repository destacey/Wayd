import {
  calendarDateInZone,
  calendarDaysBetween,
  CalendarDate,
} from './calendar-date'

/**
 * Represents the health status of an iteration (sprint or PI iteration).
 */
export enum IterationHealthStatus {
  OnTrack = 'On Track',
  AtRisk = 'At Risk',
  OffTrack = 'Off Track',
  Completed = 'Completed',
  NotStarted = 'Not Started',
  Unknown = 'Unknown',
}

/**
 * Result of the iteration health calculation.
 */
export interface IterationHealthResult {
  status: IterationHealthStatus
  variancePercent: number
}

/**
 * Parameters for calculating iteration health.
 */
export interface IterationHealthParams {
  /** Start date of the iteration */
  startDate: CalendarDate
  /** End date of the iteration */
  endDate: CalendarDate
  /** Total planned points/items */
  total: number
  /** Completed points/items */
  completed: number
  /** Optional reference day (defaults to today in the viewer's calendar) */
  referenceDate?: CalendarDate | Date
}

/**
 * A sprint's planned days, and the period it is Active when the API knows it.
 */
export interface SprintDays {
  start: CalendarDate
  end: CalendarDate
  activeFrom?: Date
  activeUntil?: Date
  timeZone?: string
}

/**
 * The days a sprint's timeline, countdown and health run over: the days it is
 * Active, in its team's zone. A sprint whose team is not mapped has no Active
 * period and falls back to its planned days.
 */
export function sprintActiveDays(sprint: SprintDays): {
  start: CalendarDate
  end: CalendarDate
} {
  const { activeFrom, activeUntil, timeZone } = sprint
  if (!activeFrom || !activeUntil || !timeZone) {
    return { start: sprint.start, end: sprint.end }
  }

  const start = calendarDateInZone(activeFrom, timeZone)
  // activeUntil is exclusive: a sprint that runs to the end of Friday ends at
  // Saturday midnight, so its last day is the one the moment before falls on.
  const end = calendarDateInZone(
    new Date(new Date(activeUntil).getTime() - 1),
    timeZone,
  )
  return { start, end: end < start ? start : end }
}

/**
 * Calculates the health status of an iteration based on burndown progress.
 *
 * The calculation compares actual progress against ideal linear burndown:
 * - On Track: Within 10% of ideal burndown
 * - At Risk: 10-25% behind ideal burndown
 * - Off Track: More than 25% behind ideal burndown
 *
 * @param params - The iteration health parameters
 * @returns The health result with status and variance percentage
 *
 * @example
 * // Sprint: 40 SP, 14 days, Day 10, 24 SP done
 * const result = calculateIterationHealth({
 *   startDate: '2024-01-01',
 *   endDate: '2024-01-14',
 *   total: 40,
 *   completed: 24,
 *   referenceDate: '2024-01-10',
 * })
 * // result.status === IterationHealthStatus.AtRisk
 * // result.variancePercent === 11.5 (behind)
 */
export function calculateIterationHealth(
  params: IterationHealthParams,
): IterationHealthResult {
  const { startDate, endDate, total, completed, referenceDate } = params

  const now = referenceDate ?? new Date()
  const daysElapsedRaw = calendarDaysBetween(startDate, now)

  // Handle iteration not yet started
  if (daysElapsedRaw <= 0) {
    return { status: IterationHealthStatus.NotStarted, variancePercent: 0 }
  }

  // Handle completed iteration (end date has passed)
  if (calendarDaysBetween(endDate, now) > 0) {
    return { status: IterationHealthStatus.Completed, variancePercent: 0 }
  }

  // Handle no work planned
  if (total <= 0) {
    return { status: IterationHealthStatus.Unknown, variancePercent: 0 }
  }

  const totalDays = calendarDaysBetween(startDate, endDate)
  const daysElapsed = Math.min(daysElapsedRaw, totalDays)
  const daysRemaining = Math.max(0, totalDays - daysElapsed)

  // Where should we be? (ideal linear burndown)
  // At day 0, idealRemaining = total
  // At end, idealRemaining = 0
  const idealRemaining = total * (daysRemaining / totalDays)

  // Where are we actually?
  const actualRemaining = total - completed

  // How far off are we? (positive = behind, negative = ahead)
  const variance = actualRemaining - idealRemaining
  const variancePercent = (variance / total) * 100

  // Determine health status based on thresholds
  if (variancePercent <= 10) {
    return { status: IterationHealthStatus.OnTrack, variancePercent }
  } else if (variancePercent <= 25) {
    return { status: IterationHealthStatus.AtRisk, variancePercent }
  } else {
    return { status: IterationHealthStatus.OffTrack, variancePercent }
  }
}
