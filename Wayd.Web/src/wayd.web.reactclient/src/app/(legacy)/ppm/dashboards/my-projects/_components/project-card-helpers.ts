import { EmployeeNavigationDto, ProjectListDto } from '@/src/services/wayd-api'
import { getInitials } from '@/src/utils'

export { getInitials }

export interface PortfolioGroup {
  portfolioId: string
  portfolioName: string
  projects: ProjectListDto[]
}

export interface TeamMemberWithRoles {
  employee: EmployeeNavigationDto
  roles: string[]
}

const STATUS_SORT_ORDER: Record<string, number> = {
  Active: 1,
  Approved: 2,
  Proposed: 3,
  Completed: 4,
  Canceled: 5,
}

function getStatusSortOrder(project: ProjectListDto): number {
  return STATUS_SORT_ORDER[project.status?.name] ?? 99
}

export function sortProjects(projects: ProjectListDto[]): ProjectListDto[] {
  return [...projects].sort((a, b) => {
    const statusDiff = getStatusSortOrder(a) - getStatusSortOrder(b)
    if (statusDiff !== 0) return statusDiff
    return a.name.localeCompare(b.name, undefined, {
      numeric: true,
      sensitivity: 'base',
    })
  })
}

export function getUserRoles(
  project: ProjectListDto,
  employeeId: string | null,
): string[] {
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


export function collectTeamMembers(
  project: ProjectListDto,
): TeamMemberWithRoles[] {
  const map = new Map<string, TeamMemberWithRoles>()

  const addRole = (employees: EmployeeNavigationDto[], role: string) => {
    for (const emp of employees) {
      const existing = map.get(emp.id)
      if (existing) {
        existing.roles.push(role)
      } else {
        map.set(emp.id, { employee: emp, roles: [role] })
      }
    }
  }

  addRole(project.projectSponsors ?? [], 'Sponsor')
  addRole(project.projectOwners ?? [], 'Owner')
  addRole(project.projectManagers ?? [], 'PM')

  return Array.from(map.values())
}
