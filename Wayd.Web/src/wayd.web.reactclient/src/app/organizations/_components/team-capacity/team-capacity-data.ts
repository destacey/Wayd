import { addCalendarDays, CalendarDate, formatCalendarDate } from '@/src/utils'
import {
  ChangeEmployeeAllocationsRequest,
  EmployeeAllocationChangeRequest,
  NavigationDto,
} from '@/src/services/wayd-api'
import {
  TeamCapacityPersonDto,
  TeamCapacityRoleDto,
  TeamCapacityRollupNodeDto,
  TeamCapacityTimeAwayDto,
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
 * A person's time away as their teammates read it, a line per stretch: the
 * days and how much of each, never why.
 */
export function timeAwayLines(timeAway: TeamCapacityTimeAwayDto[]): string[] {
  return timeAway.map((t) => {
    const days =
      t.start === t.end
        ? formatCalendarDate(t.start)
        : `${formatCalendarDate(t.start)} to ${formatCalendarDate(t.end)}`
    return `${days}: ${t.percentAway}% of the day`
  })
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

export const PERSON_TYPE_LABEL = 'Person'
export const WHOLE_RANGE_LABEL = 'Whole range'

/**
 * A row of the rollup tree: a team beneath the team of teams, or a person
 * allocated to one.
 */
export interface RollupRow {
  /** Unique in the tree. A team that moved appears once under each parent. */
  id: string
  kind: 'team' | 'person'
  name: string
  href: string
  /** The team's type, or `Person`. */
  type: string
  /** The days a team counts for. Absent for a person. */
  periods?: { start: CalendarDate; end: CalendarDate }[]
  /** A team's own working days in its periods. Absent for a person. */
  workingDays?: number
  /** For a team, everything beneath it included. */
  allocatedPersonDays: number
  deliveryPersonDays: number
  children: RollupRow[]
}

/**
 * The rollup as tree rows. Under each team come the people allocated to it
 * and then the teams beneath it, each in the order they arrive.
 */
export function rollupRows(
  nodes: TeamCapacityRollupNodeDto[],
  parentId = '',
): RollupRow[] {
  return nodes.map((node) => {
    const id = `${parentId}/${node.team.id}`

    return {
      id,
      kind: 'team',
      name: node.team.name,
      href:
        node.team.type === 'Team'
          ? `/organizations/teams/${node.team.key}`
          : `/organizations/team-of-teams/${node.team.key}`,
      type: node.team.type,
      periods: node.periods,
      workingDays: node.workingDays,
      allocatedPersonDays: node.allocatedPersonDays,
      deliveryPersonDays: node.deliveryPersonDays,
      children: [
        ...node.people.map((person): RollupRow => ({
          id: `${id}/${person.employee.id}`,
          kind: 'person',
          name: person.employee.name,
          href: `/organizations/employees/${person.employee.key}`,
          type: PERSON_TYPE_LABEL,
          allocatedPersonDays: person.allocatedPersonDays,
          deliveryPersonDays: person.deliveryPersonDays,
          children: [],
        })),
        ...rollupRows(node.children, id),
      ],
    }
  })
}

/**
 * The days a team counts for, as text: `Whole range` when it was in the
 * hierarchy throughout, or else each run of days.
 */
export function formatPeriods(
  periods: { start: CalendarDate; end: CalendarDate }[],
  range: CapacityRange,
): string {
  if (
    periods.length === 1 &&
    periods[0].start === range.from &&
    periods[0].end === range.to
  )
    return WHOLE_RANGE_LABEL

  return periods
    .map((p) =>
      p.start === p.end
        ? formatCalendarDate(p.start)
        : `${formatCalendarDate(p.start)} to ${formatCalendarDate(p.end)}`,
    )
    .join('; ')
}
