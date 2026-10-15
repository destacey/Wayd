import {
  addCalendarDays,
  CalendarDate,
  compareCalendarDates,
} from '@/src/utils'
import {
  ChangeEmployeeAllocationsRequest,
  EmployeeAllocationChangeRequest,
  NavigationDto,
  TeamCapacityPlanDto,
  TeamCapacityPlanPersonDayDto,
} from '@/src/services/wayd-api'
import { formatHours } from '../time-away/time-away-duration'

/**
 * Seven calendar days of a plan, counted from its first day, so a sprint's
 * first week is its first seven days whatever weekday it starts on. The last
 * week ends with the plan and may be shorter.
 */
export interface PlanWeek {
  /** From 0, in date order. */
  index: number
  start: CalendarDate
  end: CalendarDate
  /** The team's working days in the week, which its cells are read over. */
  days: CalendarDate[]
}

/** The weeks of a plan from `from` to `to`, each holding its working days. */
export function planWeeks(
  from: CalendarDate,
  to: CalendarDate,
  days: CalendarDate[],
): PlanWeek[] {
  const weeks: PlanWeek[] = []
  for (
    let start = from, index = 0;
    compareCalendarDates(start, to) <= 0;
    start = addCalendarDays(start, 7), index++
  ) {
    const lastOfSeven = addCalendarDays(start, 6)
    const end = compareCalendarDates(lastOfSeven, to) < 0 ? lastOfSeven : to
    weeks.push({
      index,
      start,
      end,
      days: days.filter(
        (day) =>
          compareCalendarDates(day, start) >= 0 &&
          compareCalendarDates(day, end) <= 0,
      ),
    })
  }
  return weeks
}

/** One person's time in one role, as a row of the plan grid. */
export interface PlanRow {
  /** Unique within the plan: the person and the role. */
  id: string
  employee: NavigationDto
  role?: NavigationDto
  countsTowardDeliveryCapacity: boolean
  /** The percent allocated each working day; a day left out has none. */
  percentByDate: Map<CalendarDate, number>
  /** What each day's percent is worth, in person-days. */
  personDaysByDate: Map<CalendarDate, number>
  /** The person's holidays, time away and time elsewhere, by day. */
  conditionsByDate: Map<CalendarDate, TeamCapacityPlanPersonDayDto>
  /** Whether this is the person's first row, which carries their actions. */
  isFirstOfPerson: boolean
  /** Whether the caller may record the person's time away. */
  canManageTimeAway: boolean
}

/** A row per person and role, in the order people and roles arrive in. */
export function planRows(plan: TeamCapacityPlanDto): PlanRow[] {
  const employees = new Map(
    plan.capacity.people.map((p) => [p.employee.id, p.employee]),
  )

  return plan.people.flatMap((person) => {
    const employee = employees.get(person.employeeId)
    if (!employee) return []

    const conditionsByDate = new Map(person.days.map((d) => [d.date, d]))
    return person.rows.map((row, index) => ({
      id: `${person.employeeId}|${row.role?.id ?? ''}`,
      employee,
      role: row.role,
      countsTowardDeliveryCapacity: row.countsTowardDeliveryCapacity,
      percentByDate: new Map(row.days.map((d) => [d.date, d.percent])),
      personDaysByDate: new Map(row.days.map((d) => [d.date, d.personDays])),
      conditionsByDate,
      isFirstOfPerson: index === 0,
      canManageTimeAway: person.canManageTimeAway === true,
    }))
  })
}

/** What a row holds over some of its days, as one cell shows it. */
export interface PlanCell {
  /** The lowest and highest percent allocated on the days. */
  minPercent: number
  maxPercent: number
  personDays: number
  /** How many of the days are holidays in the calendar the person takes. */
  holidays: number
  /** The days the person is away for some or all of. */
  awayDays: number
  /** The most of the person's time allocated to other teams on one of the days. */
  maxElsewherePercent: number
}

/** The cell of `row` over `days`. A cell with no days reads as 0%. */
export function planCell(row: PlanRow, days: CalendarDate[]): PlanCell {
  const percents = days.map((day) => row.percentByDate.get(day) ?? 0)
  const conditions = days
    .map((day) => row.conditionsByDate.get(day))
    .filter((c) => c !== undefined)

  return {
    minPercent: percents.length ? Math.min(...percents) : 0,
    maxPercent: percents.length ? Math.max(...percents) : 0,
    personDays: days.reduce(
      (sum, day) => sum + (row.personDaysByDate.get(day) ?? 0),
      0,
    ),
    holidays: conditions.filter((c) => c.isHoliday).length,
    awayDays: conditions.filter((c) => c.percentAway > 0).length,
    maxElsewherePercent: Math.max(
      0,
      ...conditions.map((c) => c.elsewherePercent),
    ),
  }
}

/** A cell's percent: one figure when every day has it, else the range. */
export const percentLabel = (cell: PlanCell): string =>
  cell.minPercent === cell.maxPercent
    ? `${cell.minPercent}%`
    : `${cell.minPercent}–${cell.maxPercent}%`

/**
 * A cell's time in hours of the working day: one figure when every day has
 * it, else the range. "4 h/day" is half of an 8-hour day.
 */
export const hoursLabel = (cell: PlanCell, workingDayHours: number): string => {
  const min = formatHours((cell.minPercent * workingDayHours) / 100)
  const max = formatHours((cell.maxPercent * workingDayHours) / 100)
  return min === max ? `${min} h/day` : `${min}–${max} h/day`
}

/** The key of a cell edit: a whole week, or one day. */
export const weekEditKey = (week: PlanWeek): string => `w:${week.index}`
export const dayEditKey = (day: CalendarDate): string => `d:${day}`

/** The percents entered in a row's cells, by edit key. */
export type RowEdits = Record<string, number>

/**
 * The percent a row's day will have once its edits are saved: the day's own
 * edit, else its week's, else what it has now.
 */
export function pendingPercent(
  row: PlanRow,
  week: PlanWeek,
  day: CalendarDate,
  edits: RowEdits = {},
): number {
  return (
    edits[dayEditKey(day)] ??
    edits[weekEditKey(week)] ??
    row.percentByDate.get(day) ??
    0
  )
}

/** Whether a week, or any of its days, has an edit not yet saved. */
export const weekIsEdited = (week: PlanWeek, edits: RowEdits = {}): boolean =>
  edits[weekEditKey(week)] !== undefined ||
  week.days.some((day) => edits[dayEditKey(day)] !== undefined)

/**
 * A row's edits with one cell set, or cleared when `percent` is undefined. A
 * week replaces any edits to its days, so what the week shows is what is
 * saved; a day keeps its week's edit, and its own percent wins on save.
 */
export function withCellEdit(
  edits: RowEdits,
  week: PlanWeek,
  day: CalendarDate | undefined,
  percent: number | undefined,
): RowEdits {
  const next = { ...edits }
  const key = day ? dayEditKey(day) : weekEditKey(week)
  if (!day) for (const d of week.days) delete next[dayEditKey(d)]

  if (percent === undefined) delete next[key]
  else next[key] = percent
  return next
}

/**
 * The allocation changes a row's edits make. A week sets every calendar day
 * of the week, weekend included, so the person's allocation is not split
 * around days the team does not work; a day sets that day. Days outside the
 * edited cells, and so outside the plan, are left as they were. Weeks come
 * first, so a day edited inside an edited week keeps its own percent. A cell
 * that already reads the entered percent on every day changes nothing.
 */
export function rowChanges(
  row: PlanRow,
  weeks: PlanWeek[],
  edits: RowEdits,
  teamId: string,
): EmployeeAllocationChangeRequest[] {
  const change = (start: CalendarDate, end: CalendarDate, percent: number) => ({
    teamId,
    roleId: row.role?.id,
    start,
    end,
    percent,
  })

  const weekChanges = weeks.flatMap((week) => {
    const percent = edits[weekEditKey(week)]
    if (percent === undefined) return []
    const cell = planCell(row, week.days)
    return cell.minPercent === percent && cell.maxPercent === percent
      ? []
      : [change(week.start, week.end, percent)]
  })

  const dayChanges = weeks.flatMap((week) =>
    week.days.flatMap((day) => {
      const percent = edits[dayEditKey(day)]
      if (percent === undefined) return []
      const weekPercent = edits[weekEditKey(week)]
      const current =
        weekPercent !== undefined
          ? weekPercent
          : (row.percentByDate.get(day) ?? 0)
      return current === percent ? [] : [change(day, day, percent)]
    }),
  )

  return [...weekChanges, ...dayChanges]
}

/**
 * The requests that save a plan's edits: one per person, so each person's
 * changes across their roles are made together or not at all. A person whose
 * edits change nothing has no request.
 */
export function planRequests(
  rows: PlanRow[],
  weeks: PlanWeek[],
  editsByRow: Record<string, RowEdits>,
  teamId: string,
): { employeeId: string; request: ChangeEmployeeAllocationsRequest }[] {
  const changesByEmployee = new Map<string, EmployeeAllocationChangeRequest[]>()
  for (const row of rows) {
    const edits = editsByRow[row.id]
    if (!edits) continue

    const changes = rowChanges(row, weeks, edits, teamId)
    if (!changes.length) continue

    changesByEmployee.set(row.employee.id, [
      ...(changesByEmployee.get(row.employee.id) ?? []),
      ...changes,
    ])
  }

  return [...changesByEmployee].map(([employeeId, changes]) => ({
    employeeId,
    request: { changes },
  }))
}
