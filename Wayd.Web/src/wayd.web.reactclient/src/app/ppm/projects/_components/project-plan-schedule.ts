import { ProjectPlanNodeDto } from '@/src/services/wayd-api'

const DATE_ONLY = /^(\d{4})-(\d{2})-(\d{2})/

/** The schedule position of a plan item relative to today. */
export type PlanScheduleLabel = 'Overdue' | 'Due This Week' | 'Upcoming' | null

type PlanScheduleNode = Pick<ProjectPlanNodeDto, 'status'> & {
  end?: Date | string | undefined
  plannedDate?: Date | string | undefined
}

/**
 * Resolves a plan date to a calendar day number (YYYYMMDD) for comparison.
 *
 * Plan dates are NodaTime LocalDates, so the API sends a bare "2026-09-04"
 * with no zone. `new Date()` would read that as UTC midnight, which is the
 * previous day for anyone behind UTC — a task due today would look overdue.
 * The leading date is therefore taken verbatim, never through a Date.
 */
function toCalendarDay(value: Date | string): number | null {
  if (typeof value === 'string') {
    const match = DATE_ONLY.exec(value)
    if (match) {
      return Number(match[1]) * 10000 + Number(match[2]) * 100 + Number(match[3])
    }
  }

  const date = value instanceof Date ? value : new Date(value)
  if (Number.isNaN(date.getTime())) return null

  return (
    date.getFullYear() * 10000 + (date.getMonth() + 1) * 100 + date.getDate()
  )
}

function calendarDayOf(date: Date): number {
  return (
    date.getFullYear() * 10000 + (date.getMonth() + 1) * 100 + date.getDate()
  )
}

/** The date `days` after `from`, as a calendar day number. */
function calendarDayPlus(from: Date, days: number): number {
  const shifted = new Date(
    from.getFullYear(),
    from.getMonth(),
    from.getDate() + days,
  )
  return calendarDayOf(shifted)
}

/**
 * Where a plan item sits against the schedule.
 *
 * The buckets mirror GetProjectPlanSummaryQuery exactly, so the grid column and
 * the summary counts can never disagree: the week ends on Saturday, today
 * counts as due this week rather than as a bucket of its own, and anything
 * beyond next Saturday is unlabelled. Completed and canceled items are
 * unlabelled too — a finished task is not late.
 */
export function getPlanScheduleLabel(
  node: PlanScheduleNode,
  now: Date = new Date(),
): PlanScheduleLabel {
  const statusName = node.status?.name
  if (statusName === 'Completed' || statusName === 'Canceled') return null

  const dueDate = node.end ?? node.plannedDate
  if (!dueDate) return null

  const due = toCalendarDay(dueDate)
  if (due === null) return null

  const today = calendarDayOf(now)
  if (due < today) return 'Overdue'

  // Saturday = 6 in JS (Sunday = 0), matching the query's ISO Saturday.
  const daysUntilSaturday = (6 - now.getDay() + 7) % 7
  const endOfThisWeek = calendarDayPlus(now, daysUntilSaturday)
  if (due <= endOfThisWeek) return 'Due This Week'

  const endOfNextWeek = calendarDayPlus(now, daysUntilSaturday + 7)
  if (due <= endOfNextWeek) return 'Upcoming'

  return null
}

/**
 * A plan item is overdue when its due date has passed and it is neither
 * completed nor canceled. An item due today is not overdue.
 */
export function isPlanItemOverdue(
  node: PlanScheduleNode,
  now: Date = new Date(),
): boolean {
  return getPlanScheduleLabel(node, now) === 'Overdue'
}
