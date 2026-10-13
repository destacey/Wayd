import { addCalendarDays, CalendarDate } from '@/src/utils'

/** The part of an allocation the totals are worked out from. */
export interface AllocationSpan {
  start: CalendarDate
  /** Absent when the allocation has no end. */
  end?: CalendarDate
  percent: number
}

/** A stretch of days over which the total allocated stays the same. */
export interface AllocationPeriod {
  start: CalendarDate
  /** Absent for the last period, which runs on with no end. */
  end?: CalendarDate
  percent: number
}

/** The share of the person's time allocated on `day`, across all teams. */
export function allocatedOn(
  allocations: AllocationSpan[],
  day: CalendarDate,
): number {
  return allocations
    .filter((a) => a.start <= day && (!a.end || day <= a.end))
    .reduce((total, a) => total + a.percent, 0)
}

/**
 * The total allocated from `from` on, as the fewest periods that cover every
 * day: a new period starts only where the total changes. The last has no end.
 */
export function allocationPeriods(
  allocations: AllocationSpan[],
  from: CalendarDate,
): AllocationPeriod[] {
  // The total can only change the day an allocation starts or the day after
  // one ends.
  const changeDays = new Set<CalendarDate>([from])
  for (const allocation of allocations) {
    if (allocation.start > from) changeDays.add(allocation.start)
    if (allocation.end && allocation.end >= from)
      changeDays.add(addCalendarDays(allocation.end, 1))
  }

  const periods: AllocationPeriod[] = []
  for (const day of [...changeDays].sort()) {
    const percent = allocatedOn(allocations, day)
    const previous = periods[periods.length - 1]
    if (previous?.percent === percent) continue

    if (previous) previous.end = addCalendarDays(day, -1)
    periods.push({ start: day, percent })
  }

  return periods
}
