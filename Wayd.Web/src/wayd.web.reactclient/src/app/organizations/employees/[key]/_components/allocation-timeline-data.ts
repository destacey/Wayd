import { timeAwayLabel } from '@/src/app/organizations/_components/time-away/time-away-duration'
import type {
  TimelineGroup,
  TimelineItem,
} from '@/src/components/common/timeline'
import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import type { EmployeeAllocationDto } from '@/src/store/features/organization/employee-allocations-api'
import type { EmployeeTimeAwayEntryDto } from '@/src/store/features/organization/employee-time-away-api'
import {
  addCalendarDays,
  CalendarDate,
  startOfCalendarDate as startOfDay,
} from '@/src/utils'
import { allocationPeriods } from './allocation-periods'

/** The lane every team's allocations are added up in. */
export const TOTAL_GROUP_ID = 'total'

/** The lane a person's time away from every team is drawn in. */
export const TIME_AWAY_GROUP_ID = 'time-away'

const FULL_ALLOCATION = 100
const WHOLE_DAY = 100

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
  /** A stretch the person is away from every team. */
  timeAway: string
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
 * up, a Time away lane when they have any, then one lane per team. An
 * allocation with no end is drawn to the edge of the timeline.
 */
export function allocationTimelineData(
  allocations: EmployeeAllocationDto[],
  timeAway: EmployeeTimeAwayEntryDto[],
  today: CalendarDate,
  colors: AllocationTimelineColors,
  workingDayHours = 8,
): AllocationTimelineData {
  const windowStartDay = addCalendarDays(today, -WINDOW_DAYS_BEFORE)
  const windowEndDay = addCalendarDays(today, WINDOW_DAYS_AFTER)

  const firstDay = [...allocations, ...timeAway].reduce(
    (first, a) => (a.start < first ? a.start : first),
    windowStartDay,
  )
  const lastDay = [...allocations, ...timeAway].reduce(
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

  const fixedGroups: TimelineGroup[] = [
    { id: TOTAL_GROUP_ID, label: 'Total', order: 0 },
    ...(timeAway.length > 0
      ? [{ id: TIME_AWAY_GROUP_ID, label: 'Time away', order: 1 }]
      : []),
  ]
  const groups: TimelineGroup[] = [
    ...fixedGroups,
    ...[...teams.entries()]
      .sort(([, a], [, b]) => caseInsensitiveCompare(a, b))
      .map(([id, label], index) => ({
        id,
        label,
        order: fixedGroups.length + index,
      })),
  ]

  // Without data, so a stretch of time away is not opened as an allocation.
  const timeAwayItems: TimelineItem<EmployeeAllocationDto>[] = timeAway.map(
    (entry) => ({
      id: `${TIME_AWAY_GROUP_ID}-${entry.id}`,
      kind: 'range',
      groupId: TIME_AWAY_GROUP_ID,
      start: startOfDay(entry.start),
      end: endOf(entry.end),
      label:
        entry.percentAway >= WHOLE_DAY
          ? 'Away'
          : `Away ${timeAwayLabel(entry.percentAway, workingDayHours).toLowerCase()}`,
      color: colors.timeAway,
    }),
  )

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
    items: [...totalItems, ...timeAwayItems, ...teamItems],
    windowStart: startOfDay(windowStartDay),
    windowEnd: startOfDay(windowEndDay),
    minDate: startOfDay(minDay),
    maxDate: startOfDay(maxDay),
  }
}
