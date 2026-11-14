import type { Mock } from 'vitest'
import { render, screen, within } from '@testing-library/react'
import ProjectHealthChecksDueWidget, {
  EXPIRING_SOON_DAYS,
  HEALTH_CHECKS_DUE_LIMIT,
  healthChecksDue,
  ProjectHealthChecksDueTile,
} from './project-health-checks-due-widget'
import {
  HomeContextResponse,
  ProjectListDto,
  ProjectMemberRole,
  ProjectStatus,
} from '@/src/services/wayd-api'
import { tileValueStyle } from '../attention-tile-test-utils'
import { homeContext } from '../home-context-test-utils'
import { renderReportingWidget } from '../widget-display-test-utils'

const mockHome: { context?: HomeContextResponse } = {}

vi.mock('../use-home-context', () => ({
  useHomeContext: () => ({ context: mockHome.context, isLoading: false }),
}))

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}))

vi.mock('@/src/store/features/ppm/projects-api', () => ({
  useGetProjectsQuery: vi.fn(),
}))

import { useGetProjectsQuery } from '@/src/store/features/ppm/projects-api'

const mockProjects = useGetProjectsQuery as Mock

/** Noon on 11 November 2026, local time, the day the tests run on. */
const NOW = new Date(2026, 10, 11, 12)

/** Noon local time, `days` after {@link NOW}. */
const inDays = (days: number) => new Date(2026, 10, 11 + days, 12)

const project = (
  key: string,
  name: string,
  expiration?: Date,
): ProjectListDto =>
  ({
    id: `id-${key}`,
    key,
    name,
    healthCheck: expiration
      ? { id: `hc-${key}`, status: { id: 1, name: 'Healthy' }, expiration }
      : undefined,
  }) as ProjectListDto

const loaded = (projects: ProjectListDto[], isError = false) => ({
  data: projects,
  isLoading: false,
  isError,
})

beforeEach(() => {
  vi.clearAllMocks()
  mockHome.context = undefined
  // Only Date: antd and Testing Library still need real timers.
  vi.useFakeTimers({ toFake: ['Date'] })
  vi.setSystemTime(NOW)
})

afterEach(() => {
  vi.useRealTimers()
})

describe('healthChecksDue', () => {
  it('puts projects with no current check first, by name, then those expiring soonest', () => {
    // Arrange
    const projects = [
      project('LATER', 'Later', inDays(EXPIRING_SOON_DAYS + 1)),
      project('SOON', 'Soon', inDays(5)),
      project('ZULU', 'Zulu'),
      project('SOONER', 'Sooner', inDays(1)),
      project('ALPHA', 'alpha'),
      project('EDGE', 'Edge', inDays(EXPIRING_SOON_DAYS)),
    ]

    // Act
    const due = healthChecksDue(projects, NOW)

    // Assert
    expect(due.map((d) => [d.project.key, d.expiresInDays])).toEqual([
      ['ALPHA', undefined],
      ['ZULU', undefined],
      ['SOONER', 1],
      ['SOON', 5],
      ['EDGE', EXPIRING_SOON_DAYS],
    ])
  })

  it('counts a check that lapsed after the list was read as missing', () => {
    // Arrange
    const lapsed = project('LAPSED', 'Lapsed', inDays(1))

    // Act
    const due = healthChecksDue([lapsed], inDays(2))

    // Assert
    expect(due).toEqual([{ project: lapsed }])
  })
})

describe('ProjectHealthChecksDueWidget', () => {
  it('asks for the active projects the caller owns or manages', () => {
    // Arrange
    mockProjects.mockReturnValue(loaded([]))

    // Act
    render(<ProjectHealthChecksDueWidget title="Project health checks due" />)

    // Assert
    expect(mockProjects).toHaveBeenCalledWith(
      {
        status: [ProjectStatus.Active],
        role: [ProjectMemberRole.Owner, ProjectMemberRole.Manager],
      },
      { skip: false },
    )
  })

  it('skips the request when the home context says the caller leads no project', () => {
    // Arrange
    mockHome.context = homeContext({
      projects: { totalCount: 2, memberCount: 2 },
    })
    mockProjects.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: false,
    })

    // Act
    render(<ProjectHealthChecksDueWidget title="Project health checks due" />)

    // Assert
    expect(mockProjects).toHaveBeenCalledWith(expect.anything(), { skip: true })
    expect(
      screen.getByText(/None of the projects you own or manage is active/),
    ).toBeInTheDocument()
  })

  it('marks a missing check and says when a current one expires', () => {
    // Arrange
    mockProjects.mockReturnValue(
      loaded([
        project('APOLLO', 'Apollo'),
        project('GEMINI', 'Gemini', inDays(1)),
      ]),
    )

    // Act
    render(<ProjectHealthChecksDueWidget title="Project health checks due" />)

    // Assert
    const items = screen.getAllByRole('listitem')
    expect(
      within(items[0]).getByRole('link', { name: 'APOLLO · Apollo' }),
    ).toHaveAttribute('href', '/ppm/projects/APOLLO')
    expect(
      within(items[0]).getByText('No current health check'),
    ).toBeInTheDocument()
    expect(within(items[1]).getByText('Expires in 1 day')).toBeInTheDocument()
  })

  it('lists the first projects and counts the rest', () => {
    // Arrange
    mockProjects.mockReturnValue(
      loaded(
        Array.from({ length: HEALTH_CHECKS_DUE_LIMIT + 2 }, (_, i) =>
          project(`P${i}`, `Project ${i}`),
        ),
      ),
    )

    // Act
    render(<ProjectHealthChecksDueWidget title="Project health checks due" />)

    // Assert
    expect(screen.getAllByRole('listitem')).toHaveLength(
      HEALTH_CHECKS_DUE_LIMIT,
    )
    expect(screen.getByText('And 2 more.')).toBeInTheDocument()
  })

  it('says a check expires today when it ends later today', () => {
    // Arrange
    mockProjects.mockReturnValue(
      loaded([project('APOLLO', 'Apollo', new Date(2026, 10, 11, 18))]),
    )

    // Act
    render(<ProjectHealthChecksDueWidget title="Project health checks due" />)

    // Assert
    expect(screen.getByText('Expires today')).toBeInTheDocument()
  })

  it('shows a check that lapsed while the page was open as expired', () => {
    // Arrange
    mockProjects.mockReturnValue(
      loaded([project('APOLLO', 'Apollo', new Date(2026, 10, 11, 9))]),
    )

    // Act
    render(<ProjectHealthChecksDueWidget title="Project health checks due" />)

    // Assert
    expect(screen.getByText('Health check expired')).toBeInTheDocument()
  })

  it('says every check is current when none is due', () => {
    // Arrange
    mockProjects.mockReturnValue(
      loaded([project('APOLLO', 'Apollo', inDays(30))]),
    )

    // Act
    render(<ProjectHealthChecksDueWidget title="Project health checks due" />)

    // Assert
    expect(screen.queryAllByRole('listitem')).toHaveLength(0)
    expect(screen.getByText(/has a current health check/)).toBeInTheDocument()
  })

  it('lists checks expiring within a longer window when set to', () => {
    // Arrange
    mockProjects.mockReturnValue(
      loaded([
        project('SOON', 'Soon', inDays(12)),
        project('LATER', 'Later', inDays(20)),
      ]),
    )

    // Act
    render(
      <ProjectHealthChecksDueWidget
        title="Project health checks due"
        settings={{ 'expiring-window': '14' }}
      />,
    )

    // Assert
    expect(screen.getAllByRole('listitem')).toHaveLength(1)
    expect(screen.getByText('Expires in 12 days')).toBeInTheDocument()
  })

  it.each([
    [
      'loading',
      { data: undefined, isLoading: true, isError: false },
      'loading',
    ],
    ['failed', { data: undefined, isLoading: false, isError: true }, 'failed'],
    ['empty when no active project is led', loaded([]), 'empty'],
    [
      'empty when every led project has a check lasting beyond the expiring window',
      loaded([
        project('APOLLO', 'Apollo', inDays(EXPIRING_SOON_DAYS + 1)),
        project('GEMINI', 'Gemini', inDays(30)),
      ]),
      'empty',
    ],
    [
      'rows when a led project has no current check',
      loaded([project('APOLLO', 'Apollo')]),
      'rows',
    ],
    [
      'rows when a led project has a check expiring within the window',
      loaded([project('APOLLO', 'Apollo', inDays(EXPIRING_SOON_DAYS))]),
      'rows',
    ],
  ])('reports %s to the page', (_name, query, expected) => {
    // Arrange
    mockProjects.mockReturnValue(query)

    // Act
    const { reportedState } = renderReportingWidget(
      <ProjectHealthChecksDueWidget title="Project health checks due" />,
    )

    // Assert
    expect(reportedState()).toBe(expected)
  })

  it('reports empty when the home context says the caller leads no project and the request is skipped', () => {
    // Arrange
    mockHome.context = homeContext({
      projects: { totalCount: 2, memberCount: 2 },
    })
    mockProjects.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: false,
    })

    // Act
    const { reportedState } = renderReportingWidget(
      <ProjectHealthChecksDueWidget title="Project health checks due" />,
    )

    // Assert
    expect(reportedState()).toBe('empty')
  })
})

describe('ProjectHealthChecksDueTile', () => {
  it('counts a check that lapsed while the page was open', () => {
    // Arrange
    mockProjects.mockReturnValue(
      loaded([
        project('APOLLO', 'Apollo', new Date(2026, 10, 11, 9)),
        project('GEMINI', 'Gemini', inDays(3)),
      ]),
    )

    // Act
    render(<ProjectHealthChecksDueTile />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Health checks due on projects you lead: 1',
      }),
    ).toBeInTheDocument()
  })

  it('counts only the projects with no current check, in the warning colour', () => {
    // Arrange
    mockProjects.mockReturnValue(
      loaded([
        project('APOLLO', 'Apollo'),
        project('ZULU', 'Zulu'),
        project('GEMINI', 'Gemini', inDays(1)),
      ]),
    )

    // Act
    const { container } = render(<ProjectHealthChecksDueTile />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Health checks due on projects you lead: 2',
      }),
    ).toBeInTheDocument()
    expect(tileValueStyle(container)).toContain('var(--ant-color-warning)')
  })

  it('counts zero without asking when the caller leads no project', () => {
    // Arrange
    mockHome.context = homeContext()
    mockProjects.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: false,
    })

    // Act
    render(<ProjectHealthChecksDueTile />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Health checks due on projects you lead: 0',
      }),
    ).toBeInTheDocument()
  })

  it('shows a dash, not the last good count, after a failed refetch', () => {
    // Arrange
    mockProjects.mockReturnValue(loaded([project('APOLLO', 'Apollo')], true))

    // Act
    render(<ProjectHealthChecksDueTile />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Health checks due on projects you lead: unavailable',
      }),
    ).toBeInTheDocument()
  })
})
