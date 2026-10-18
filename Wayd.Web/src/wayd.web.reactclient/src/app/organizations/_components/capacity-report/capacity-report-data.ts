import { addCalendarDays, CalendarDate } from '@/src/utils'
import {
  CapacityMeasuresDto,
  TeamCapacityReportDto,
  TeamCapacityReportLineDto,
  TeamCapacityReportTeamDto,
} from '@/src/store/features/organization/team-capacity-api'
import { CapacityRange } from '../team-capacity/team-capacity-data'

/** How the report's rows are grouped. */
export type CapacityReportGrouping = 'team' | 'person'

/** The unit person-days are shown in. */
export type CapacityReportUnit = 'days' | 'hours'

/** Time in person-days, as the report's lines carry it. */
export type Measures = CapacityMeasuresDto

export const ZERO: Measures = {
  allocatedPersonDays: 0,
  holidayPersonDays: 0,
  timeAwayPersonDays: 0,
  availablePersonDays: 0,
  deliveryPersonDays: 0,
}

export const addMeasures = (a: Measures, b: Measures): Measures => ({
  allocatedPersonDays: a.allocatedPersonDays + b.allocatedPersonDays,
  holidayPersonDays: a.holidayPersonDays + b.holidayPersonDays,
  timeAwayPersonDays: a.timeAwayPersonDays + b.timeAwayPersonDays,
  availablePersonDays: a.availablePersonDays + b.availablePersonDays,
  deliveryPersonDays: a.deliveryPersonDays + b.deliveryPersonDays,
})

const sumMeasures = (all: Measures[]): Measures => all.reduce(addMeasures, ZERO)

/** Bucket by bucket sums of equally long lists. */
const sumBuckets = (all: Measures[][], count: number): Measures[] =>
  Array.from({ length: count }, (_, i) =>
    sumMeasures(all.map((buckets) => buckets[i] ?? ZERO)),
  )

/** What a row of the report is. */
export type CapacityReportRowKind = 'team' | 'direct' | 'person'

/** A row of the report's grid: a team, a person, or a team of teams' own allocations. */
export interface CapacityReportRow {
  id: string
  kind: CapacityReportRowKind
  name: string
  /** Where the name links to; absent for a row that is not a record. */
  href?: string
  /** The team's type, or 'Person'. */
  type: string
  /** The team member roles the row's time is in, for a person row. */
  roles?: string
  total: Measures
  buckets: Measures[]
  /** A person's time allocated outside the report, on a person row grouped by person. */
  outsidePersonDays?: number
  /** A person's time allocated to no team, on a person row grouped by person. */
  unallocatedPersonDays?: number
  children?: CapacityReportRow[]
  /** The names from the top row down to this one, which an export carries in place of the tree. */
  path: string
}

const PATH_SEPARATOR = ' > '

const teamHref = (team: TeamCapacityReportTeamDto['team']) =>
  team.type === 'Team'
    ? `/organizations/teams/${team.key}`
    : `/organizations/team-of-teams/${team.key}`

const roleNames = (lines: TeamCapacityReportLineDto[]): string =>
  [...new Set(lines.map((l) => l.role?.name ?? 'No role'))].join(', ')

/** Lines grouped by a key, keeping the order the keys first appear in. */
function groupBy<T>(items: T[], key: (item: T) => string): Map<string, T[]> {
  const groups = new Map<string, T[]>()
  for (const item of items) {
    const k = key(item)
    groups.set(k, [...(groups.get(k) ?? []), item])
  }
  return groups
}

/**
 * The report as a tree of teams: each team with its people beneath it, and a
 * team of teams with its own allocations as a row of their own beside the
 * teams beneath it. The one top row is the team the report is for, and its
 * total is the report's.
 */
export function rowsByTeam(report: TeamCapacityReportDto): CapacityReportRow[] {
  const bucketCount = report.buckets.length
  const people = new Map(report.people.map((p) => [p.employee.id, p.employee]))
  const linesByNode = groupBy(report.lines, (l) => String(l.nodeId))
  const childrenByNode = groupBy(
    report.teams.filter((t) => t.parentNodeId != null),
    (t) => String(t.parentNodeId),
  )

  const personRows = (
    nodeId: number,
    parentPath: string,
  ): CapacityReportRow[] =>
    [
      ...groupBy(linesByNode.get(String(nodeId)) ?? [], (l) => l.employeeId),
    ].map(([employeeId, lines]) => {
      const employee = people.get(employeeId)
      const name = employee?.name ?? ''
      return {
        id: `n${nodeId}-p${employeeId}`,
        kind: 'person',
        name,
        path: parentPath + PATH_SEPARATOR + name,
        href: employee ? `/organizations/employees/${employee.key}` : undefined,
        type: 'Person',
        roles: roleNames(lines),
        total: sumMeasures(lines.map((l) => l.total)),
        buckets: sumBuckets(
          lines.map((l) => l.buckets),
          bucketCount,
        ),
      }
    })

  const teamRow = (
    node: TeamCapacityReportTeamDto,
    parentPath?: string,
  ): CapacityReportRow => {
    const path =
      parentPath === undefined
        ? node.team.name
        : parentPath + PATH_SEPARATOR + node.team.name
    const teams = (childrenByNode.get(String(node.nodeId)) ?? []).map((child) =>
      teamRow(child, path),
    )

    // A team of teams' own people are set apart from the teams beneath it, so
    // its direct time reads as one figure.
    const directName = 'Allocated directly'
    const setApart = teams.length > 0
    const direct = personRows(
      node.nodeId,
      setApart ? path + PATH_SEPARATOR + directName : path,
    )
    const children =
      setApart && direct.length > 0
        ? [
            {
              id: `n${node.nodeId}-direct`,
              kind: 'direct' as const,
              name: directName,
              path: path + PATH_SEPARATOR + directName,
              type: node.team.type,
              total: sumMeasures(direct.map((r) => r.total)),
              buckets: sumBuckets(
                direct.map((r) => r.buckets),
                bucketCount,
              ),
              children: direct,
            },
            ...teams,
          ]
        : [...direct, ...teams]

    return {
      id: `n${node.nodeId}`,
      kind: 'team',
      name: node.team.name,
      path,
      href: teamHref(node.team),
      type: node.team.type,
      total: sumMeasures(children.map((r) => r.total)),
      buckets: sumBuckets(
        children.map((r) => r.buckets),
        bucketCount,
      ),
      children,
    }
  }

  const root = report.teams.find((t) => t.parentNodeId == null)
  return root ? [teamRow(root)] : []
}

/**
 * The report as a list of people: each person with every team in the report
 * they have time on beneath them, a team that sits in the hierarchy at two
 * places counted as one, and their time outside the report and unallocated.
 */
export function rowsByPerson(
  report: TeamCapacityReportDto,
): CapacityReportRow[] {
  const bucketCount = report.buckets.length
  const teams = new Map(report.teams.map((t) => [t.nodeId, t.team]))
  const linesByPerson = groupBy(report.lines, (l) => l.employeeId)

  return report.people
    .filter((person) => linesByPerson.has(person.employee.id))
    .map((person) => {
      const lines = linesByPerson.get(person.employee.id)!
      const children = [
        ...groupBy(lines, (l) => teams.get(l.nodeId)?.id ?? ''),
      ].map(([teamId, teamLines]): CapacityReportRow => {
        const team = teams.get(teamLines[0].nodeId)!
        return {
          id: `p${person.employee.id}-t${teamId}`,
          kind: 'team',
          name: team.name,
          path: person.employee.name + PATH_SEPARATOR + team.name,
          href: teamHref(team),
          type: team.type,
          roles: roleNames(teamLines),
          total: sumMeasures(teamLines.map((l) => l.total)),
          buckets: sumBuckets(
            teamLines.map((l) => l.buckets),
            bucketCount,
          ),
        }
      })

      return {
        id: `p${person.employee.id}`,
        kind: 'person',
        name: person.employee.name,
        path: person.employee.name,
        href: `/organizations/employees/${person.employee.key}`,
        type: 'Person',
        roles: roleNames(lines),
        total: sumMeasures(children.map((r) => r.total)),
        buckets: sumBuckets(
          children.map((r) => r.buckets),
          bucketCount,
        ),
        outsidePersonDays: person.outsidePersonDays,
        unallocatedPersonDays: person.unallocatedPersonDays,
        children,
      }
    })
}

/** The total of the rows at the top of either grouping. */
export const grandTotal = (rows: CapacityReportRow[]): Measures =>
  sumMeasures(rows.map((r) => r.total))

/** Person-days in the chosen unit, to two decimal places at most, without trailing zeros. */
export function formatCapacity(
  personDays: number,
  unit: CapacityReportUnit,
  workingDayHours: number,
): string {
  const value = unit === 'hours' ? personDays * workingDayHours : personDays
  return String(Number(value.toFixed(2)))
}

/** The first to the last day of the month before the one `today` falls in. */
export function previousMonthOf(today: CalendarDate): CapacityRange {
  const lastOfPrevious = addCalendarDays(`${today.slice(0, 7)}-01`, -1)
  return { from: `${lastOfPrevious.slice(0, 7)}-01`, to: lastOfPrevious }
}

/** The calendar quarter `today` falls in, or the one `offset` quarters from it. */
export function quarterOf(today: CalendarDate, offset = 0): CapacityRange {
  const year = Number(today.slice(0, 4))
  const quarter = Math.floor((Number(today.slice(5, 7)) - 1) / 3) + offset
  const startYear = year + Math.floor(quarter / 4)
  const startMonth = (((quarter % 4) + 4) % 4) * 3 + 1
  const from = `${startYear}-${String(startMonth).padStart(2, '0')}-01`
  const nextStartMonth = startMonth + 3
  const next =
    nextStartMonth > 12
      ? `${startYear + 1}-01-01`
      : `${startYear}-${String(nextStartMonth).padStart(2, '0')}-01`
  return { from, to: addCalendarDays(next, -1) }
}
