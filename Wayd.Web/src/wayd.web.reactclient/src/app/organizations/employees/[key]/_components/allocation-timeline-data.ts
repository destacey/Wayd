import type {
  TimelineGroup,
  TimelineItem,
} from '@/src/components/common/timeline'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import type { EmployeeAllocationDto } from '@/src/store/features/organization/employee-allocations-api'
import {
  addCalendarDays,
  CalendarDate,
  startOfCalendarDate as startOfDay,
} from '@/src/utils'
import { allocationPeriods } from './allocation-periods'

/** The lane every team's allocations are added up in. */
export const TOTAL_GROUP_ID = 'total'

const FULL_ALLOCATION = 100

/** Days shown either side of today when the timeline opens. */
const WINDOW_DAYS_BEFORE = 30
const WINDOW_DAYS_AFTER = 180

/** Room left beyond the first and last allocation, so neither sits on the edge. */
const MARGIN_DAYS = 30

export interface AllocationTimelineColors {
  /** An allocation to a team. */
  allocation: string
  /** A stretch where some of the person's time is allocated. */
  partTotal: string
  /** A stretch where all of the person's time is allocated. */
  fullTotal: string
}

export interface AllocationTimelineData {
  groups: TimelineGroup[]
  items: TimelineItem<EmployeeAllocationDto>[]
  windowStart: number
  windowEnd: number
  minDate: number
  maxDate: number
}

/**
 * A person's allocations as timeline lanes: a Total lane adding every team
 * up, then one lane per team. An allocation with no end is drawn to the edge
 * of the timeline.
 */
export function allocationTimelineData(
  allocations: EmployeeAllocationDto[],
  today: CalendarDate,
  colors: AllocationTimelineColors,
): AllocationTimelineData {
  const windowStartDay = addCalendarDays(today, -WINDOW_DAYS_BEFORE)
  const windowEndDay = addCalendarDays(today, WINDOW_DAYS_AFTER)

  const firstDay = allocations.reduce(
    (first, a) => (a.start < first ? a.start : first),
    windowStartDay,
  )
  const lastDay = allocations.reduce(
    (last, a) => (a.end && a.end > last ? a.end : last),
    windowEndDay,
  )
  const minDay = addCalendarDays(firstDay, -MARGIN_DAYS)
  const maxDay = addCalendarDays(lastDay, MARGIN_DAYS)

  // A bar's end is exclusive, so it ends at the start of the day after.
  const endOf = (end: CalendarDate | undefined): number =>
    startOfDay(end ? addCalendarDays(end, 1) : maxDay)

  const teams = new Map<string, string>()
  for (const allocation of allocations)
    teams.set(allocation.team.id, allocation.team.name)

  const groups: TimelineGroup[] = [
    { id: TOTAL_GROUP_ID, label: 'Total', order: 0 },
    ...[...teams.entries()]
      .sort(([, a], [, b]) => caseInsensitiveCompare(a, b))
      .map(([id, label], index) => ({ id, label, order: index + 1 })),
  ]

  const teamItems: TimelineItem<EmployeeAllocationDto>[] = allocations.map(
    (allocation) => ({
      id: allocation.id,
      kind: 'range',
      groupId: allocation.team.id,
      start: startOfDay(allocation.start),
      end: endOf(allocation.end),
      label: allocation.role
        ? `${allocation.percent}% ${allocation.role.name}`
        : `${allocation.percent}%`,
      color: colors.allocation,
      data: allocation,
    }),
  )

  const totalItems: TimelineItem<EmployeeAllocationDto>[] = allocationPeriods(
    allocations,
    firstDay,
  )
    .filter((period) => period.percent > 0)
    .map((period) => ({
      id: `${TOTAL_GROUP_ID}-${period.start}`,
      kind: 'range',
      groupId: TOTAL_GROUP_ID,
      start: startOfDay(period.start),
      end: endOf(period.end),
      label: `${period.percent}%`,
      color:
        period.percent >= FULL_ALLOCATION ? colors.fullTotal : colors.partTotal,
    }))

  return {
    groups,
    items: [...totalItems, ...teamItems],
    windowStart: startOfDay(windowStartDay),
    windowEnd: startOfDay(windowEndDay),
    minDate: startOfDay(minDay),
    maxDate: startOfDay(maxDay),
  }
}
