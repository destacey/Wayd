import { addCalendarDays, CalendarDate } from '@/src/utils'
import {
  ChangeEmployeeAllocationsRequest,
  EmployeeAllocationChangeRequest,
  NavigationDto,
} from '@/src/services/wayd-api'
import {
  TeamCapacityPersonDto,
  TeamCapacityRoleDto,
} from '@/src/store/features/organization/team-capacity-api'

/** An inclusive range of calendar dates. */
export interface CapacityRange {
  from: CalendarDate
  to: CalendarDate
}

/** The first to the last day of the month `today` falls in. */
export function monthOf(today: CalendarDate): CapacityRange {
  const year = Number(today.slice(0, 4))
  const month = Number(today.slice(5, 7))
  const firstOfNext =
    month === 12
      ? `${year + 1}-01-01`
      : `${year}-${String(month + 1).padStart(2, '0')}-01`

  return {
    from: `${today.slice(0, 7)}-01`,
    to: addCalendarDays(firstOfNext, -1),
  }
}

/** Person-days to two decimal places at most, without trailing zeros. */
export const formatPersonDays = (personDays: number): string =>
  String(Number(personDays.toFixed(2)))

/** One allocation of one person, as a row of the people grid. */
export interface CapacityRow {
  /** The allocation's id. */
  id: string
  person: TeamCapacityPersonDto
  role?: NavigationDto
  countsTowardDeliveryCapacity: boolean
  start: CalendarDate
  /** Absent when the allocation has no end. */
  end?: CalendarDate
  percent: number
  personDays: number
}

/** A row per allocation, keeping the order people and allocations arrive in. */
export function capacityRows(people: TeamCapacityPersonDto[]): CapacityRow[] {
  return people.flatMap((person) =>
    person.allocations.map((allocation) => ({
      id: allocation.id,
      person,
      role: allocation.role,
      countsTowardDeliveryCapacity: allocation.countsTowardDeliveryCapacity,
      start: allocation.start,
      end: allocation.end,
      percent: allocation.percent,
      personDays: allocation.personDays,
    })),
  )
}

/**
 * The days of `range` an allocation covers, which is what a change to it from
 * this range starts from.
 */
export function clipToRange(
  allocation: { start: CalendarDate; end?: CalendarDate },
  range: CapacityRange,
): { start: CalendarDate; end: CalendarDate } {
  return {
    start: allocation.start > range.from ? allocation.start : range.from,
    end:
      allocation.end && allocation.end < range.to ? allocation.end : range.to,
  }
}

/** A line of the capacity-by-role summary. */
export interface RoleLine {
  key: string
  label: string
  personDays: number
  /** False for the one line that gathers roles outside delivery capacity. */
  delivery: boolean
}

export const NO_ROLE_LABEL = 'No role'
export const NON_DELIVERY_LABEL = 'Non-delivery'

/**
 * Capacity by role: a line per role that counts toward delivery capacity,
 * then one line for all the time in roles that do not, so the lines add up to
 * everything allocated.
 */
export function roleLines(roles: TeamCapacityRoleDto[]): RoleLine[] {
  const lines: RoleLine[] = roles
    .filter((r) => r.countsTowardDeliveryCapacity)
    .map((r) => ({
      key: r.role?.id ?? 'no-role',
      label: r.role?.name ?? NO_ROLE_LABEL,
      personDays: r.personDays,
      delivery: true,
    }))

  const nonDelivery = roles.filter((r) => !r.countsTowardDeliveryCapacity)
  if (nonDelivery.length > 0) {
    lines.push({
      key: 'non-delivery',
      label: NON_DELIVERY_LABEL,
      personDays: nonDelivery.reduce((total, r) => total + r.personDays, 0),
      delivery: false,
    })
  }

  return lines
}

/** A proposed allocation the planner has settled on. */
export interface ProposedAllocation {
  employeeId: string
  roleId: string
  /** The last day, or absent for no end. */
  end?: CalendarDate
  percent: number
}

/**
 * The requests that save a confirmed proposal: one per person, since a
 * person's allocations change together. Each allocation starts on `from` and
 * runs to its row's end. A row at 0% proposes nothing and is left out.
 */
export function proposalRequests(
  rows: ProposedAllocation[],
  teamId: string,
  from: CalendarDate,
): { employeeId: string; request: ChangeEmployeeAllocationsRequest }[] {
  const changesByEmployee = new Map<string, EmployeeAllocationChangeRequest[]>()
  for (const row of rows) {
    if (row.percent <= 0) continue

    const changes = changesByEmployee.get(row.employeeId) ?? []
    changes.push({
      teamId,
      roleId: row.roleId,
      start: from,
      end: row.end,
      percent: row.percent,
    })
    changesByEmployee.set(row.employeeId, changes)
  }

  return [...changesByEmployee].map(([employeeId, changes]) => ({
    employeeId,
    request: { changes },
  }))
}

/**
 * The people whose proposed rows add up to more than they have left, by
 * employee id.
 */
export function overProposed(
  rows: (ProposedAllocation & { remainingPercent: number })[],
): Set<string> {
  const totals = new Map<string, { proposed: number; remaining: number }>()
  for (const row of rows) {
    const total = totals.get(row.employeeId) ?? {
      proposed: 0,
      remaining: row.remainingPercent,
    }
    total.proposed += row.percent
    totals.set(row.employeeId, total)
  }

  return new Set(
    [...totals]
      .filter(([, total]) => total.proposed > total.remaining)
      .map(([employeeId]) => employeeId),
  )
}
