import dayjs from 'dayjs'
import { EmployeeNavigationDto, ProjectListDto } from '@/src/services/wayd-api'

// The global dayjs stub formats but cannot compare or diff dates, which is
// exactly what the ending-soon rule and the end-date sort do.
jest.unmock('dayjs')
import {
  collectLeadership,
  computeAttention,
  getEmployeeRoles,
  groupProjects,
  isEndingSoon,
  matchesAttention,
  matchesSearch,
  NO_HEALTH_CHECK_LABEL,
  NO_PROGRAM_LABEL,
  PlanSummaries,
  scopeFromSearchParams,
  scopeIsComplete,
  scopeToSearchParams,
  sortProjects,
  summarizeGroup,
} from './dashboard-model'

const today = dayjs('2026-09-24')

const employee = (id: string, name: string): EmployeeNavigationDto =>
  ({ id, key: 1, name }) as EmployeeNavigationDto

const project = (
  overrides: Partial<ProjectListDto> & { key: string },
): ProjectListDto =>
  ({
    id: `id-${overrides.key}`,
    name: overrides.key,
    status: { id: 2, name: 'Active', lifecycleCategory: 'Active' },
    portfolio: { id: 'port-a', key: 1, name: 'Portfolio A' },
    projectSponsors: [],
    projectOwners: [],
    projectManagers: [],
    projectMembers: [],
    strategicThemes: [],
    stages: [],
    rank: 1,
    canManageProject: false,
    ...overrides,
  }) as ProjectListDto

const health = (name: string) => ({ id: 'hc', status: { id: 1, name } })

const summary = (overdue: number) => ({
  overdue,
  dueThisWeek: 0,
  upcoming: 0,
  totalLeafTasks: 5,
})

describe('scope in the URL', () => {
  it('reads each scope from its own parameter, an empty value meaning nothing chosen yet', () => {
    // Arrange / Act / Assert
    expect(scopeFromSearchParams(new URLSearchParams(), true)).toEqual({
      kind: 'me',
    })
    expect(
      scopeFromSearchParams(new URLSearchParams('employee=ada'), true),
    ).toEqual({ kind: 'person', employeeId: 'ada' })
    expect(
      scopeFromSearchParams(new URLSearchParams('portfolio='), true),
    ).toEqual({ kind: 'portfolio', portfolioId: null })
    expect(
      scopeFromSearchParams(new URLSearchParams('program=prog-1'), true),
    ).toEqual({ kind: 'program', programId: 'prog-1' })
    expect(
      scopeFromSearchParams(new URLSearchParams('scope=all'), true),
    ).toEqual({ kind: 'all' })
  })

  it('starts an unlinked account on the person picker instead of Me', () => {
    expect(scopeFromSearchParams(new URLSearchParams(), false)).toEqual({
      kind: 'person',
      employeeId: null,
    })
  })

  it('writes one parameter per scope and clears the others, keeping unrelated ones', () => {
    // Arrange
    const current = new URLSearchParams('employee=ada&other=1')

    // Act
    const portfolio = scopeToSearchParams(
      { kind: 'portfolio', portfolioId: 'port-1' },
      current,
    )
    const me = scopeToSearchParams({ kind: 'me' }, current)
    const all = scopeToSearchParams({ kind: 'all' }, current)

    // Assert
    expect(portfolio.toString()).toBe('other=1&portfolio=port-1')
    expect(me.toString()).toBe('other=1')
    expect(all.toString()).toBe('other=1&scope=all')
  })

  it('knows when a scope still needs a record chosen', () => {
    expect(scopeIsComplete({ kind: 'me' })).toBe(true)
    expect(scopeIsComplete({ kind: 'all' })).toBe(true)
    expect(scopeIsComplete({ kind: 'person', employeeId: null })).toBe(false)
    expect(scopeIsComplete({ kind: 'program', programId: 'p' })).toBe(true)
  })
})

describe('isEndingSoon', () => {
  it('is true for an open project ending within 30 days, today included', () => {
    // Arrange
    const soon = project({ key: 'A', end: new Date(2026, 9, 24) })
    const todayEnd = project({ key: 'B', end: new Date(2026, 8, 24) })

    // Act / Assert
    expect(isEndingSoon(soon, today)).toBe(true)
    expect(isEndingSoon(todayEnd, today)).toBe(true)
  })

  it('is false for a project already past its end, undated, or further out', () => {
    // Arrange
    const past = project({ key: 'A', end: new Date(2026, 8, 23) })
    const undated = project({ key: 'B' })
    const later = project({ key: 'C', end: new Date(2026, 9, 25) })

    // Act / Assert
    expect(isEndingSoon(past, today)).toBe(false)
    expect(isEndingSoon(undated, today)).toBe(false)
    expect(isEndingSoon(later, today)).toBe(false)
  })

  it('is false for a closed project, whose end describes finished work', () => {
    // Arrange
    const completed = project({
      key: 'A',
      end: new Date(2026, 9, 1),
      status: { id: 3, name: 'Completed', lifecycleCategory: 'Completed' },
    })

    // Act / Assert
    expect(isEndingSoon(completed, today)).toBe(false)
  })
})

describe('computeAttention', () => {
  it('counts each signal from the projects and their plan summaries', () => {
    // Arrange
    const projects = [
      project({ key: 'A', healthCheck: health('Unhealthy') }),
      project({ key: 'B', healthCheck: health('At Risk') }),
      project({ key: 'C', end: new Date(2026, 9, 10) }),
      project({
        key: 'D',
        healthCheck: health('Healthy'),
        end: new Date(2027, 0, 1),
      }),
    ]
    const summaries: PlanSummaries = {
      'id-A': summary(3),
      'id-B': summary(2),
      'id-C': summary(0),
    }

    // Act
    const counts = computeAttention(projects, summaries, today)

    // Assert
    expect(counts).toEqual({
      inScope: 4,
      unhealthy: 1,
      atRisk: 1,
      overdueTasks: 5,
      overdueProjects: 2,
      noHealthCheck: 1,
      endingSoon: 1,
    })
  })

  it('does not count a closed project as missing its health check', () => {
    // Arrange
    const closed = project({
      key: 'A',
      status: { id: 4, name: 'Canceled', lifecycleCategory: 'Canceled' },
    })

    // Act
    const counts = computeAttention([closed], {}, today)

    // Assert
    expect(counts.noHealthCheck).toBe(0)
  })
})

describe('matchesAttention', () => {
  const unhealthy = project({ key: 'A', healthCheck: health('Unhealthy') })
  const quiet = project({ key: 'B', healthCheck: health('Healthy') })
  const summaries: PlanSummaries = { 'id-A': summary(1) }

  it('keeps everything for the all filter', () => {
    expect(matchesAttention(quiet, 'all', summaries, today)).toBe(true)
  })

  it('keeps only projects carrying the chosen signal', () => {
    expect(matchesAttention(unhealthy, 'unhealthy', summaries, today)).toBe(
      true,
    )
    expect(matchesAttention(quiet, 'unhealthy', summaries, today)).toBe(false)
    expect(matchesAttention(unhealthy, 'overdue', summaries, today)).toBe(true)
    expect(matchesAttention(quiet, 'overdue', summaries, today)).toBe(false)
    expect(matchesAttention(quiet, 'noHealthCheck', summaries, today)).toBe(
      false,
    )
    expect(
      matchesAttention(
        project({ key: 'C' }),
        'noHealthCheck',
        summaries,
        today,
      ),
    ).toBe(true)
  })
})

describe('matchesSearch', () => {
  it('matches the name or the key, ignoring case and surrounding space', () => {
    // Arrange
    const p = project({ key: 'CP-104', name: 'Unified Billing Portal' })

    // Act / Assert
    expect(matchesSearch(p, ' billing ')).toBe(true)
    expect(matchesSearch(p, 'cp-10')).toBe(true)
    expect(matchesSearch(p, 'mobile')).toBe(false)
    expect(matchesSearch(p, '')).toBe(true)
  })
})

describe('sortProjects', () => {
  it('puts the worst health first, then the most overdue, then the name', () => {
    // Arrange
    const projects = [
      project({ key: 'Z', name: 'Zeta', healthCheck: health('Healthy') }),
      project({ key: 'B', name: 'Beta', healthCheck: health('Unhealthy') }),
      project({ key: 'A', name: 'Alpha', healthCheck: health('Unhealthy') }),
      project({ key: 'R', name: 'Rho', healthCheck: health('At Risk') }),
      project({ key: 'N', name: 'Nu' }),
    ]
    const summaries: PlanSummaries = { 'id-B': summary(4), 'id-A': summary(1) }

    // Act
    const sorted = sortProjects(projects, 'attention', summaries)

    // Assert: unhealthy B (4 overdue) before A (1), then at risk, healthy, not reported
    expect(sorted.map((p) => p.key)).toEqual(['B', 'A', 'R', 'Z', 'N'])
  })

  it('sorts undated projects last by end date and unscored last by score', () => {
    // Arrange
    const projects = [
      project({ key: 'A', end: new Date(2026, 11, 1) }),
      project({ key: 'B' }),
      project({ key: 'C', end: new Date(2026, 9, 1) }),
    ]
    const scored = [
      project({ key: 'A' }),
      project({
        key: 'B',
        currentScore: { value: 70 } as ProjectListDto['currentScore'],
      }),
      project({
        key: 'C',
        currentScore: { value: 90 } as ProjectListDto['currentScore'],
      }),
    ]

    // Act / Assert
    expect(sortProjects(projects, 'end', {}).map((p) => p.key)).toEqual([
      'C',
      'A',
      'B',
    ])
    expect(sortProjects(scored, 'score', {}).map((p) => p.key)).toEqual([
      'C',
      'B',
      'A',
    ])
  })
})

describe('groupProjects', () => {
  const portfolioB = { id: 'port-b', key: 2, name: 'Portfolio B' }
  const projects = [
    project({ key: 'A', portfolio: portfolioB }),
    project({
      key: 'B',
      program: { id: 'prog-1', key: 1, name: 'Payments' },
      healthCheck: health('At Risk'),
    }),
    project({
      key: 'C',
      status: { id: 5, name: 'Approved', lifecycleCategory: 'NotStarted' },
      healthCheck: health('Unhealthy'),
    }),
  ]

  it('groups by portfolio in name order', () => {
    // Act
    const groups = groupProjects(projects, 'portfolio', 'name', {})

    // Assert
    expect(groups.map((g) => g.name)).toEqual(['Portfolio A', 'Portfolio B'])
    expect(groups[0].projects.map((p) => p.key)).toEqual(['B', 'C'])
  })

  it('puts projects without a program under their own heading, last', () => {
    // Act
    const groups = groupProjects(projects, 'program', 'name', {})

    // Assert
    expect(groups.map((g) => g.name)).toEqual(['Payments', NO_PROGRAM_LABEL])
    expect(groups[1].projects.map((p) => p.key)).toEqual(['A', 'C'])
  })

  it('orders health groups worst first and status groups active first', () => {
    // Act
    const byHealth = groupProjects(projects, 'health', 'name', {})
    const byStatus = groupProjects(projects, 'status', 'name', {})

    // Assert
    expect(byHealth.map((g) => g.name)).toEqual([
      'Unhealthy',
      'At Risk',
      NO_HEALTH_CHECK_LABEL,
    ])
    expect(byStatus.map((g) => g.name)).toEqual(['Active', 'Approved'])
  })

  it('summarises what needs attention in each group', () => {
    // Arrange
    const summaries: PlanSummaries = { 'id-B': summary(2), 'id-C': summary(1) }

    // Act / Assert
    expect(summarizeGroup(projects, summaries)).toBe(
      '1 unhealthy · 1 at risk · 3 overdue tasks',
    )
    expect(summarizeGroup([projects[0]], summaries)).toBe(
      'Nothing needs attention',
    )
  })
})

describe('getEmployeeRoles', () => {
  it('lists every role the employee holds, in leadership order', () => {
    // Arrange
    const me = employee('me', 'Me')
    const p = project({
      key: 'A',
      projectOwners: [me],
      projectManagers: [me],
      projectMembers: [employee('other', 'Other')],
    })

    // Act / Assert
    expect(getEmployeeRoles(p, 'me')).toEqual(['Owner', 'PM'])
    expect(getEmployeeRoles(p, 'other')).toEqual(['Member'])
    expect(getEmployeeRoles(p, null)).toEqual([])
  })
})

describe('collectLeadership', () => {
  it('lists each leader once with all their roles, and leaves members out', () => {
    // Arrange
    const ada = employee('ada', 'Ada')
    const bob = employee('bob', 'Bob')
    const p = project({
      key: 'A',
      projectSponsors: [ada],
      projectOwners: [ada, bob],
      projectMembers: [employee('cy', 'Cy')],
    })

    // Act
    const leadership = collectLeadership(p)

    // Assert
    expect(leadership).toEqual([
      { employee: ada, roles: ['Sponsor', 'Owner'] },
      { employee: bob, roles: ['Owner'] },
    ])
  })
})
