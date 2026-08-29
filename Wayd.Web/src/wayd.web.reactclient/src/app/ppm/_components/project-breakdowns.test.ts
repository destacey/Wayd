import { ProjectListDto } from '@/src/services/wayd-api'
import {
  getHealthBreakdown,
  getStatusBreakdown,
  getThemeBreakdown,
  NO_HEALTH_LABEL,
  NO_THEME_LABEL,
} from './project-breakdowns'

const tokens = {
  colorInfo: '#info',
  colorSuccess: '#success',
  colorError: '#error',
  colorWarning: '#warning',
  colorTextSecondary: '#secondary',
}

const buildProject = (
  overrides: Partial<ProjectListDto> = {},
): ProjectListDto =>
  ({
    id: 'project-id',
    key: 'PRJ-1',
    name: 'Project',
    status: { id: 2, name: 'Active', lifecycleCategory: 'Active' },
    strategicThemes: [],
    ...overrides,
  }) as unknown as ProjectListDto

const theme = (id: string, name: string) => ({ id, name }) as never

describe('getThemeBreakdown', () => {
  it('counts a project once under each theme it serves', () => {
    // Arrange
    const projects = [
      buildProject({
        id: 'a',
        strategicThemes: [
          theme('t1', 'Cloud Migration'),
          theme('t2', 'Cost Reduction'),
        ],
      }),
      buildProject({ id: 'b', strategicThemes: [theme('t1', 'Cloud Migration')] }),
    ]

    // Act
    const result = getThemeBreakdown(projects)

    // Assert
    expect(result).toEqual([
      { type: 'Cloud Migration', count: 2 },
      { type: 'Cost Reduction', count: 1 },
    ])
  })

  it('counts projects with no themes under their own label', () => {
    // Arrange
    const projects = [
      buildProject({ id: 'a', strategicThemes: [theme('t1', 'Cloud Migration')] }),
      buildProject({ id: 'b', strategicThemes: [] }),
      buildProject({ id: 'c', strategicThemes: [] }),
    ]

    // Act
    const result = getThemeBreakdown(projects)

    // Assert
    expect(result).toEqual([
      { type: 'Cloud Migration', count: 1 },
      { type: NO_THEME_LABEL, count: 2 },
    ])
  })

  it('places the untagged slice last even when it sorts first alphabetically', () => {
    // Arrange
    const projects = [
      buildProject({ id: 'a', strategicThemes: [] }),
      buildProject({ id: 'b', strategicThemes: [theme('t1', 'Zebra Program')] }),
      buildProject({ id: 'c', strategicThemes: [theme('t2', 'Alpha Program')] }),
    ]

    // Act
    const result = getThemeBreakdown(projects)

    // Assert
    expect(result.map((d) => d.type)).toEqual([
      'Alpha Program',
      'Zebra Program',
      NO_THEME_LABEL,
    ])
  })

  it('sorts named themes case-insensitively', () => {
    // Arrange
    const projects = [
      buildProject({ id: 'a', strategicThemes: [theme('t1', 'zeta')] }),
      buildProject({ id: 'b', strategicThemes: [theme('t2', 'Alpha')] }),
      buildProject({ id: 'c', strategicThemes: [theme('t3', 'beta')] }),
    ]

    // Act
    const result = getThemeBreakdown(projects)

    // Assert
    expect(result.map((d) => d.type)).toEqual(['Alpha', 'beta', 'zeta'])
  })

  it('returns nothing for an empty project set', () => {
    // Arrange
    const projects: ProjectListDto[] = []

    // Act
    const result = getThemeBreakdown(projects)

    // Assert
    expect(result).toEqual([])
  })
})

describe('getStatusBreakdown', () => {
  it('counts projects by status name', () => {
    // Arrange
    const projects = [
      buildProject({
        id: 'a',
        status: { id: 2, name: 'Active', lifecycleCategory: 'Active' } as never,
      }),
      buildProject({
        id: 'b',
        status: { id: 2, name: 'Active', lifecycleCategory: 'Active' } as never,
      }),
      buildProject({
        id: 'c',
        status: {
          id: 3,
          name: 'Completed',
          lifecycleCategory: 'Completed',
        } as never,
      }),
    ]

    // Act
    const result = getStatusBreakdown(projects, tokens)

    // Assert
    expect(result.map(({ type, count }) => ({ type, count }))).toEqual([
      { type: 'Active', count: 2 },
      { type: 'Completed', count: 1 },
    ])
  })

  it('colors each status from its lifecycle category', () => {
    // Arrange
    const projects = [
      buildProject({
        id: 'a',
        status: { id: 2, name: 'Active', lifecycleCategory: 'Active' } as never,
      }),
      buildProject({
        id: 'b',
        status: {
          id: 3,
          name: 'Completed',
          lifecycleCategory: 'Completed',
        } as never,
      }),
      buildProject({
        id: 'c',
        status: {
          id: 4,
          name: 'Canceled',
          lifecycleCategory: 'Canceled',
        } as never,
      }),
      buildProject({
        id: 'd',
        status: {
          id: 1,
          name: 'Proposed',
          lifecycleCategory: 'Proposed',
        } as never,
      }),
    ]

    // Act
    const result = getStatusBreakdown(projects, tokens)

    // Assert
    expect(
      Object.fromEntries(result.map((d) => [d.type, d.color])),
    ).toEqual({
      Active: tokens.colorInfo,
      Completed: tokens.colorSuccess,
      Canceled: tokens.colorError,
      Proposed: tokens.colorTextSecondary,
    })
  })

  it('returns nothing for an empty project set', () => {
    // Arrange
    const projects: ProjectListDto[] = []

    // Act
    const result = getStatusBreakdown(projects, tokens)

    // Assert
    expect(result).toEqual([])
  })
})

describe('breakdowns across entity types', () => {
  it('groups by status name, so colliding enum ids cannot merge states', () => {
    // Arrange — id 2 is Active on a project but Approved on a strategic
    // initiative. Grouping on the id would report them as one status.
    const project = buildProject({
      id: 'p1',
      status: { id: 2, name: 'Active', lifecycleCategory: 'Active' } as never,
    })
    const initiative = {
      id: 'si1',
      status: { id: 2, name: 'Approved', lifecycleCategory: 'NotStarted' },
      strategicThemes: [],
    } as never

    // Act
    const result = getStatusBreakdown([project, initiative], tokens)

    // Assert
    expect(result.map(({ type, count }) => ({ type, count }))).toEqual([
      { type: 'Active', count: 1 },
      { type: 'Approved', count: 1 },
    ])
  })

  it('breaks programs down by theme with the same helper as projects', () => {
    // Arrange — programs are a separate DTO carrying the same theme shape.
    const programs = [
      { strategicThemes: [{ name: 'Cloud Migration' }] },
      { strategicThemes: [] },
    ]

    // Act
    const result = getThemeBreakdown(programs)

    // Assert
    expect(result).toEqual([
      { type: 'Cloud Migration', count: 1 },
      { type: NO_THEME_LABEL, count: 1 },
    ])
  })
})

const healthTokens = {
  colorSuccess: '#success',
  colorWarning: '#warning',
  colorError: '#error',
  colorTextDisabled: '#disabled',
}

const withHealth = (
  id: string,
  status: string | null,
  lifecycleCategory = 'Active',
) =>
  buildProject({
    id,
    status: { id: 2, name: 'Active', lifecycleCategory } as never,
    healthCheck: status
      ? ({ id: `hc-${id}`, status: { id: 1, name: status } } as never)
      : undefined,
  })

describe('getHealthBreakdown', () => {
  it('counts projects by their current health', () => {
    // Arrange
    const projects = [
      withHealth('a', 'Healthy'),
      withHealth('b', 'Healthy'),
      withHealth('c', 'At Risk'),
    ]

    // Act
    const result = getHealthBreakdown(projects, healthTokens)

    // Assert
    expect(result.map(({ type, count }) => ({ type, count }))).toEqual([
      { type: 'At Risk', count: 1 },
      { type: 'Healthy', count: 2 },
    ])
  })

  it('counts projects with no health check under their own label', () => {
    // Arrange — an unreported project is the answer most worth seeing, so it
    // is counted rather than dropped.
    const projects = [withHealth('a', 'Healthy'), withHealth('b', null)]

    // Act
    const result = getHealthBreakdown(projects, healthTokens)

    // Assert
    expect(result.map(({ type, count }) => ({ type, count }))).toEqual([
      { type: 'Healthy', count: 1 },
      { type: NO_HEALTH_LABEL, count: 1 },
    ])
  })

  it('orders worst health first, with the unreported slice last', () => {
    // Arrange
    const projects = [
      withHealth('a', 'Healthy'),
      withHealth('b', null),
      withHealth('c', 'Unhealthy'),
      withHealth('d', 'At Risk'),
    ]

    // Act
    const result = getHealthBreakdown(projects, healthTokens)

    // Assert
    expect(result.map((d) => d.type)).toEqual([
      'Unhealthy',
      'At Risk',
      'Healthy',
      NO_HEALTH_LABEL,
    ])
  })

  it('colors each slice to match the health tags on the rows', () => {
    // Arrange
    const projects = [
      withHealth('a', 'Healthy'),
      withHealth('b', 'At Risk'),
      withHealth('c', 'Unhealthy'),
      withHealth('d', null),
    ]

    // Act
    const result = getHealthBreakdown(projects, healthTokens)

    // Assert
    expect(Object.fromEntries(result.map((d) => [d.type, d.color]))).toEqual({
      Healthy: healthTokens.colorSuccess,
      'At Risk': healthTokens.colorWarning,
      Unhealthy: healthTokens.colorError,
      [NO_HEALTH_LABEL]: healthTokens.colorTextDisabled,
    })
  })


  it('excludes completed and canceled projects', () => {
    // Arrange — a closed project's last health check describes work that is
    // over, so counting it buries the health of what is actually running.
    const projects = [
      withHealth('a', 'Healthy'),
      withHealth('b', 'Unhealthy', 'Completed'),
      withHealth('c', 'Unhealthy', 'Canceled'),
    ]

    // Act
    const result = getHealthBreakdown(projects, healthTokens)

    // Assert
    expect(result.map(({ type, count }) => ({ type, count }))).toEqual([
      { type: 'Healthy', count: 1 },
    ])
  })

  it('counts projects that have not started, which are still ahead of us', () => {
    // Arrange
    const projects = [withHealth('a', null, 'NotStarted')]

    // Act
    const result = getHealthBreakdown(projects, healthTokens)

    // Assert
    expect(result).toEqual([
      { type: NO_HEALTH_LABEL, count: 1, color: healthTokens.colorTextDisabled },
    ])
  })

  it('returns nothing when every project is closed', () => {
    // Arrange
    const projects = [
      withHealth('a', 'Healthy', 'Completed'),
      withHealth('b', 'At Risk', 'Canceled'),
    ]

    // Act
    const result = getHealthBreakdown(projects, healthTokens)

    // Assert
    expect(result).toEqual([])
  })
  it('returns nothing for an empty project set', () => {
    // Arrange / Act
    const result = getHealthBreakdown([], healthTokens)

    // Assert
    expect(result).toEqual([])
  })
})
