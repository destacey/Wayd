import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import {
  EmployeeNavigationDto,
  ProjectListDto,
  ProjectPlanSummaryDto,
} from '@/src/services/wayd-api'
import dayjs, { Dayjs } from 'dayjs'

/**
 * Who or what the dashboard is looking at. `me` is the signed-in user's linked
 * employee; `person` is any employee, chosen from the scope bar.
 */
export type DashboardScope =
  { kind: 'me' } | { kind: 'person'; employeeId: string | null }

export const PROJECT_STATUS = {
  Proposed: 1,
  Active: 2,
  Completed: 3,
  Canceled: 4,
  Approved: 5,
} as const

export const DEFAULT_STATUSES: number[] = [
  PROJECT_STATUS.Approved,
  PROJECT_STATUS.Active,
]

export const ALL_ROLES: number[] = [1, 2, 3, 4, 5]

export const ROLE_OPTIONS = [
  { label: 'Sponsor', value: 1 },
  { label: 'Owner', value: 2 },
  { label: 'PM', value: 3 },
  { label: 'Member', value: 4 },
  { label: 'Task Assignee', value: 5 },
] as const

export type AttentionFilter =
  'all' | 'unhealthy' | 'atRisk' | 'overdue' | 'noHealthCheck' | 'endingSoon'

export type GroupBy = 'portfolio' | 'program' | 'health' | 'status'

export type SortBy = 'attention' | 'name' | 'end' | 'score'

export const ENDING_SOON_DAYS = 30

export type PlanSummaries = Record<string, ProjectPlanSummaryDto>

export interface AttentionCounts {
  inScope: number
  unhealthy: number
  atRisk: number
  /** Open tasks past their planned end, summed across the projects in scope. */
  overdueTasks: number
  /** How many projects those overdue tasks sit on. */
  overdueProjects: number
  noHealthCheck: number
  endingSoon: number
}

const HEALTH_RANK: Record<string, number> = {
  Unhealthy: 0,
  'At Risk': 1,
  Healthy: 2,
}

/** The label for a project with no current health check. */
export const NO_HEALTH_CHECK_LABEL = 'Not reported'

export const healthName = (project: ProjectListDto): string =>
  project.healthCheck?.status.name ?? NO_HEALTH_CHECK_LABEL

const healthRank = (project: ProjectListDto): number =>
  HEALTH_RANK[project.healthCheck?.status.name ?? ''] ?? 3

const isClosed = (project: ProjectListDto) =>
  project.status.lifecycleCategory === 'Completed' ||
  project.status.lifecycleCategory === 'Canceled'

/**
 * A project ends soon when its planned end falls within the next
 * `ENDING_SOON_DAYS` days, today included. Closed projects never do — their
 * end date describes work that is over.
 */
export const isEndingSoon = (project: ProjectListDto, today: Dayjs) => {
  if (!project.end || isClosed(project)) return false
  const end = dayjs(project.end)
  return (
    !end.isBefore(today, 'day') &&
    end.diff(today.startOf('day'), 'day') <= ENDING_SOON_DAYS
  )
}

/**
 * The list DTO carries only the current, unexpired health check, so a missing
 * one covers both "never reported" and "expired".
 */
const hasNoHealthCheck = (project: ProjectListDto) =>
  !project.healthCheck && !isClosed(project)

const overdueOn = (project: ProjectListDto, summaries: PlanSummaries) =>
  summaries[project.id]?.overdue ?? 0

export const computeAttention = (
  projects: ProjectListDto[],
  summaries: PlanSummaries,
  today: Dayjs,
): AttentionCounts => {
  const counts: AttentionCounts = {
    inScope: projects.length,
    unhealthy: 0,
    atRisk: 0,
    overdueTasks: 0,
    overdueProjects: 0,
    noHealthCheck: 0,
    endingSoon: 0,
  }

  for (const project of projects) {
    const health = project.healthCheck?.status.name
    if (health === 'Unhealthy') counts.unhealthy++
    if (health === 'At Risk') counts.atRisk++
    if (hasNoHealthCheck(project)) counts.noHealthCheck++
    if (isEndingSoon(project, today)) counts.endingSoon++
    const overdue = overdueOn(project, summaries)
    if (overdue > 0) {
      counts.overdueTasks += overdue
      counts.overdueProjects++
    }
  }

  return counts
}

export const matchesAttention = (
  project: ProjectListDto,
  filter: AttentionFilter,
  summaries: PlanSummaries,
  today: Dayjs,
): boolean => {
  switch (filter) {
    case 'all':
      return true
    case 'unhealthy':
      return project.healthCheck?.status.name === 'Unhealthy'
    case 'atRisk':
      return project.healthCheck?.status.name === 'At Risk'
    case 'overdue':
      return overdueOn(project, summaries) > 0
    case 'noHealthCheck':
      return hasNoHealthCheck(project)
    case 'endingSoon':
      return isEndingSoon(project, today)
  }
}

export const matchesSearch = (project: ProjectListDto, needle: string) => {
  const term = needle.trim().toLowerCase()
  if (!term) return true
  return (
    project.name.toLowerCase().includes(term) ||
    project.key.toLowerCase().includes(term)
  )
}

const STATUS_ORDER: Record<string, number> = {
  Active: 0,
  Approved: 1,
  Proposed: 2,
  Completed: 3,
  Canceled: 4,
}

const compareName = (a: ProjectListDto, b: ProjectListDto) => {
  const byName = caseInsensitiveCompare(a.name, b.name)
  // Keys are unique, so this is what makes the order total.
  return byName !== 0 ? byName : caseInsensitiveCompare(a.key, b.key)
}

const compareEnd = (a: ProjectListDto, b: ProjectListDto) => {
  // An undated project sorts last: it cannot be "ending" anything.
  if (!a.end && !b.end) return 0
  if (!a.end) return 1
  if (!b.end) return -1
  return dayjs(a.end).valueOf() - dayjs(b.end).valueOf()
}

/**
 * Orders projects within a group.
 *
 * `attention` is worst first: health, then overdue task count, then name. The
 * other modes fall back to name so that ties are stable, and `score` puts an
 * unscored project last rather than treating it as zero.
 */
export const sortProjects = (
  projects: ProjectListDto[],
  sortBy: SortBy,
  summaries: PlanSummaries,
): ProjectListDto[] =>
  [...projects].sort((a, b) => {
    switch (sortBy) {
      case 'attention': {
        const byHealth = healthRank(a) - healthRank(b)
        if (byHealth !== 0) return byHealth
        const byOverdue = overdueOn(b, summaries) - overdueOn(a, summaries)
        if (byOverdue !== 0) return byOverdue
        return compareName(a, b)
      }
      case 'end': {
        const byEnd = compareEnd(a, b)
        return byEnd !== 0 ? byEnd : compareName(a, b)
      }
      case 'score': {
        const aScore = a.currentScore?.value ?? Number.NEGATIVE_INFINITY
        const bScore = b.currentScore?.value ?? Number.NEGATIVE_INFINITY
        if (aScore !== bScore) return bScore - aScore
        return compareName(a, b)
      }
      case 'name':
        return compareName(a, b)
    }
  })

export interface ProjectGroup {
  key: string
  name: string
  projects: ProjectListDto[]
  /** "2 unhealthy · 5 overdue tasks", or the all-clear. */
  summary: string
}

export const NO_PROGRAM_LABEL = 'No program'

const groupKeyAndName = (
  project: ProjectListDto,
  groupBy: GroupBy,
): [string, string] => {
  switch (groupBy) {
    case 'portfolio':
      return [project.portfolio.id, project.portfolio.name]
    case 'program':
      return project.program
        ? [project.program.id, project.program.name]
        : ['no-program', NO_PROGRAM_LABEL]
    case 'health':
      return [healthName(project), healthName(project)]
    case 'status':
      return [project.status.name, project.status.name]
  }
}

const HEALTH_GROUP_ORDER = [
  'Unhealthy',
  'At Risk',
  'Healthy',
  NO_HEALTH_CHECK_LABEL,
]

const compareGroups = (
  a: ProjectGroup,
  b: ProjectGroup,
  groupBy: GroupBy,
): number => {
  switch (groupBy) {
    case 'health':
      return (
        HEALTH_GROUP_ORDER.indexOf(a.name) - HEALTH_GROUP_ORDER.indexOf(b.name)
      )
    case 'status':
      return (STATUS_ORDER[a.name] ?? 99) - (STATUS_ORDER[b.name] ?? 99)
    case 'program':
      // Projects held directly by a portfolio sort last rather than under
      // an empty heading at the top.
      if (a.name === NO_PROGRAM_LABEL) return 1
      if (b.name === NO_PROGRAM_LABEL) return -1
      return caseInsensitiveCompare(a.name, b.name)
    case 'portfolio':
      return caseInsensitiveCompare(a.name, b.name)
  }
}

export const summarizeGroup = (
  projects: ProjectListDto[],
  summaries: PlanSummaries,
): string => {
  const unhealthy = projects.filter(
    (p) => p.healthCheck?.status.name === 'Unhealthy',
  ).length
  const atRisk = projects.filter(
    (p) => p.healthCheck?.status.name === 'At Risk',
  ).length
  const overdue = projects.reduce((sum, p) => sum + overdueOn(p, summaries), 0)

  const parts: string[] = []
  if (unhealthy) parts.push(`${unhealthy} unhealthy`)
  if (atRisk) parts.push(`${atRisk} at risk`)
  if (overdue)
    parts.push(`${overdue} overdue ${overdue === 1 ? 'task' : 'tasks'}`)

  return parts.length ? parts.join(' · ') : 'Nothing needs attention'
}

export const groupProjects = (
  projects: ProjectListDto[],
  groupBy: GroupBy,
  sortBy: SortBy,
  summaries: PlanSummaries,
): ProjectGroup[] => {
  const map = new Map<string, ProjectGroup>()

  for (const project of projects) {
    const [key, name] = groupKeyAndName(project, groupBy)
    let group = map.get(key)
    if (!group) {
      group = { key, name, projects: [], summary: '' }
      map.set(key, group)
    }
    group.projects.push(project)
  }

  return [...map.values()]
    .map((group) => ({
      ...group,
      projects: sortProjects(group.projects, sortBy, summaries),
      summary: summarizeGroup(group.projects, summaries),
    }))
    .sort((a, b) => compareGroups(a, b, groupBy))
}

/** The project roles an employee holds, as the short labels the role chips use. */
export const getEmployeeRoles = (
  project: ProjectListDto,
  employeeId: string | null,
): string[] => {
  if (!employeeId) return []

  const roles: string[] = []
  if (project.projectSponsors?.some((e) => e.id === employeeId))
    roles.push('Sponsor')
  if (project.projectOwners?.some((e) => e.id === employeeId))
    roles.push('Owner')
  if (project.projectManagers?.some((e) => e.id === employeeId))
    roles.push('PM')
  if (project.projectMembers?.some((e) => e.id === employeeId))
    roles.push('Member')

  return roles
}

export interface TeamMemberWithRoles {
  employee: EmployeeNavigationDto
  roles: string[]
}

/** Leadership on the project, each person once with every role they hold. */
export const collectLeadership = (
  project: ProjectListDto,
): TeamMemberWithRoles[] => {
  const map = new Map<string, TeamMemberWithRoles>()

  const addRole = (employees: EmployeeNavigationDto[], role: string) => {
    for (const employee of employees) {
      const existing = map.get(employee.id)
      if (existing) {
        existing.roles.push(role)
      } else {
        map.set(employee.id, { employee, roles: [role] })
      }
    }
  }

  addRole(project.projectSponsors ?? [], 'Sponsor')
  addRole(project.projectOwners ?? [], 'Owner')
  addRole(project.projectManagers ?? [], 'PM')

  return [...map.values()]
}
