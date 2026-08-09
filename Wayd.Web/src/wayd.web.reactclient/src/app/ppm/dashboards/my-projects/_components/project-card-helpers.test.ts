import {
  sortProjects,
  getUserRoles,
  collectTeamMembers,
} from './project-card-helpers'
import {
  EmployeeNavigationDto,
  ProjectListDto,
} from '@/src/services/wayd-api'

// --- Factories ---

function createEmployee(
  id: string,
  name: string,
): EmployeeNavigationDto {
  return { id, key: 1, name } as EmployeeNavigationDto
}

function createProject(
  overrides: Partial<ProjectListDto> & { name: string },
): ProjectListDto {
  return {
    id: 'proj-1',
    key: 'P1',
    status: { name: 'Active', lifecycleCategory: 'Active' } as any,
    portfolio: { id: 'port-1', key: 1, name: 'Portfolio' },
    projectSponsors: [],
    projectOwners: [],
    projectManagers: [],
    projectMembers: [],
    strategicThemes: [],
    phases: [],
    ...overrides,
  } as ProjectListDto
}

// --- sortProjects ---

describe('sortProjects', () => {
  it('sorts by status priority (Active first)', () => {
    const projects = [
      createProject({ name: 'C', status: { name: 'Proposed' } as any }),
      createProject({ name: 'A', status: { name: 'Active' } as any }),
      createProject({ name: 'B', status: { name: 'Approved' } as any }),
    ]

    const sorted = sortProjects(projects)

    expect(sorted.map((p) => p.status.name)).toEqual([
      'Active',
      'Approved',
      'Proposed',
    ])
  })

  it('sorts alphabetically by name within the same status', () => {
    const projects = [
      createProject({ name: 'Zebra', status: { name: 'Active' } as any }),
      createProject({ name: 'Alpha', status: { name: 'Active' } as any }),
      createProject({ name: 'Mango', status: { name: 'Active' } as any }),
    ]

    const sorted = sortProjects(projects)

    expect(sorted.map((p) => p.name)).toEqual(['Alpha', 'Mango', 'Zebra'])
  })

  it('sorts by status first, then name', () => {
    const projects = [
      createProject({ name: 'B Active', status: { name: 'Active' } as any }),
      createProject({
        name: 'A Completed',
        status: { name: 'Completed' } as any,
      }),
      createProject({
        name: 'A Active',
        status: { name: 'Active' } as any,
      }),
    ]

    const sorted = sortProjects(projects)

    expect(sorted.map((p) => p.name)).toEqual([
      'A Active',
      'B Active',
      'A Completed',
    ])
  })

  it('does not mutate the original array', () => {
    const projects = [
      createProject({ name: 'B', status: { name: 'Proposed' } as any }),
      createProject({ name: 'A', status: { name: 'Active' } as any }),
    ]
    const original = [...projects]

    sortProjects(projects)

    expect(projects[0].name).toBe(original[0].name)
    expect(projects[1].name).toBe(original[1].name)
  })

  it('handles unknown status by sorting last', () => {
    const projects = [
      createProject({ name: 'Unknown', status: { name: 'Custom' } as any }),
      createProject({ name: 'Active', status: { name: 'Active' } as any }),
    ]

    const sorted = sortProjects(projects)

    expect(sorted.map((p) => p.name)).toEqual(['Active', 'Unknown'])
  })

  it('sorts embedded numbers in natural order', () => {
    const projects = [
      createProject({ name: 'Project 10', status: { name: 'Active' } as any }),
      createProject({ name: 'Project 2', status: { name: 'Active' } as any }),
      createProject({ name: 'Project 1', status: { name: 'Active' } as any }),
    ]

    const sorted = sortProjects(projects)

    expect(sorted.map((p) => p.name)).toEqual([
      'Project 1',
      'Project 2',
      'Project 10',
    ])
  })

  it('handles all five statuses in correct order', () => {
    const projects = [
      createProject({
        name: 'Canceled',
        status: { name: 'Canceled' } as any,
      }),
      createProject({
        name: 'Completed',
        status: { name: 'Completed' } as any,
      }),
      createProject({
        name: 'Proposed',
        status: { name: 'Proposed' } as any,
      }),
      createProject({ name: 'Active', status: { name: 'Active' } as any }),
      createProject({
        name: 'Approved',
        status: { name: 'Approved' } as any,
      }),
    ]

    const sorted = sortProjects(projects)

    expect(sorted.map((p) => p.status.name)).toEqual([
      'Active',
      'Approved',
      'Proposed',
      'Completed',
      'Canceled',
    ])
  })
})

// --- getUserRoles ---

describe('getUserRoles', () => {
  const emp1 = createEmployee('emp-1', 'Alice')
  const emp2 = createEmployee('emp-2', 'Bob')

  it('returns empty array when employeeId is null', () => {
    const project = createProject({
      name: 'Test',
      projectSponsors: [emp1],
    })

    expect(getUserRoles(project, null)).toEqual([])
  })

  it('returns Sponsor when user is a sponsor', () => {
    const project = createProject({
      name: 'Test',
      projectSponsors: [emp1],
    })

    expect(getUserRoles(project, 'emp-1')).toEqual(['Sponsor'])
  })

  it('returns Owner when user is an owner', () => {
    const project = createProject({
      name: 'Test',
      projectOwners: [emp1],
    })

    expect(getUserRoles(project, 'emp-1')).toEqual(['Owner'])
  })

  it('returns PM when user is a manager', () => {
    const project = createProject({
      name: 'Test',
      projectManagers: [emp1],
    })

    expect(getUserRoles(project, 'emp-1')).toEqual(['PM'])
  })

  it('returns Member when user is a member', () => {
    const project = createProject({
      name: 'Test',
      projectMembers: [emp1],
    })

    expect(getUserRoles(project, 'emp-1')).toEqual(['Member'])
  })

  it('returns multiple roles when user has multiple', () => {
    const project = createProject({
      name: 'Test',
      projectSponsors: [emp1],
      projectManagers: [emp1],
    })

    expect(getUserRoles(project, 'emp-1')).toEqual(['Sponsor', 'PM'])
  })

  it('returns empty array when user is not on the project', () => {
    const project = createProject({
      name: 'Test',
      projectSponsors: [emp2],
      projectOwners: [emp2],
    })

    expect(getUserRoles(project, 'emp-1')).toEqual([])
  })

  it('returns all four roles when user has all', () => {
    const project = createProject({
      name: 'Test',
      projectSponsors: [emp1],
      projectOwners: [emp1],
      projectManagers: [emp1],
      projectMembers: [emp1],
    })

    expect(getUserRoles(project, 'emp-1')).toEqual([
      'Sponsor',
      'Owner',
      'PM',
      'Member',
    ])
  })
})

// --- collectTeamMembers ---

describe('collectTeamMembers', () => {
  const alice = createEmployee('emp-1', 'Alice')
  const bob = createEmployee('emp-2', 'Bob')
  const charlie = createEmployee('emp-3', 'Charlie')

  it('collects sponsors, owners, and managers', () => {
    const project = createProject({
      name: 'Test',
      projectSponsors: [alice],
      projectOwners: [bob],
      projectManagers: [charlie],
    })

    const members = collectTeamMembers(project)

    expect(members).toHaveLength(3)
    expect(members.map((m) => m.employee.name)).toEqual([
      'Alice',
      'Bob',
      'Charlie',
    ])
  })

  it('assigns correct roles', () => {
    const project = createProject({
      name: 'Test',
      projectSponsors: [alice],
      projectOwners: [bob],
    })

    const members = collectTeamMembers(project)

    expect(members.find((m) => m.employee.id === 'emp-1')?.roles).toEqual([
      'Sponsor',
    ])
    expect(members.find((m) => m.employee.id === 'emp-2')?.roles).toEqual([
      'Owner',
    ])
  })

  it('merges multiple roles for the same person', () => {
    const project = createProject({
      name: 'Test',
      projectSponsors: [alice],
      projectManagers: [alice],
    })

    const members = collectTeamMembers(project)

    expect(members).toHaveLength(1)
    expect(members[0].roles).toEqual(['Sponsor', 'PM'])
  })

  it('does not include members (only leadership)', () => {
    const project = createProject({
      name: 'Test',
      projectSponsors: [alice],
      projectMembers: [bob],
    })

    const members = collectTeamMembers(project)

    expect(members).toHaveLength(1)
    expect(members[0].employee.name).toBe('Alice')
  })

  it('returns empty array when no leadership assigned', () => {
    const project = createProject({
      name: 'Test',
    })

    expect(collectTeamMembers(project)).toEqual([])
  })

  it('deduplicates across role arrays', () => {
    const project = createProject({
      name: 'Test',
      projectSponsors: [alice],
      projectOwners: [alice],
      projectManagers: [alice],
    })

    const members = collectTeamMembers(project)

    expect(members).toHaveLength(1)
    expect(members[0].roles).toEqual(['Sponsor', 'Owner', 'PM'])
  })
})
