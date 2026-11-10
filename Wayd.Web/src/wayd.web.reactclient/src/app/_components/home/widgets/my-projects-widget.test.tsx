import type { Mock } from 'vitest'
import { render, screen } from '@testing-library/react'
import MyProjectsWidget, {
  DueThisWeekTile,
  OverdueTasksTile,
  roleCounts,
} from './my-projects-widget'
import {
  HomeContextResponse,
  MyProjectsSummaryDto,
  ProjectStatus,
} from '@/src/services/wayd-api'
import { tileValueStyle } from '../attention-tile-test-utils'
import { homeContext } from '../home-context-test-utils'

const mockHome: { context?: HomeContextResponse } = {}

vi.mock('../use-home-context', () => ({
  useHomeContext: () => ({ context: mockHome.context, isLoading: false }),
}))

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}))

vi.mock('@/src/store/features/ppm/projects-api', () => ({
  useGetMyProjectsSummaryQuery: vi.fn(),
  useGetMyProjectsTaskMetricsQuery: vi.fn(),
}))

import {
  useGetMyProjectsSummaryQuery,
  useGetMyProjectsTaskMetricsQuery,
} from '@/src/store/features/ppm/projects-api'

const mockSummary = useGetMyProjectsSummaryQuery as Mock
const mockMetrics = useGetMyProjectsTaskMetricsQuery as Mock

const summary = (
  overrides: Partial<MyProjectsSummaryDto> = {},
): MyProjectsSummaryDto => ({
  totalCount: 7,
  sponsorCount: 1,
  ownerCount: 0,
  managerCount: 3,
  memberCount: 0,
  assigneeCount: 4,
  ...overrides,
})

const loaded = (data: MyProjectsSummaryDto) => ({
  data,
  isLoading: false,
  isError: false,
})

const metrics = (overdue: number, dueThisWeek: number, isError = false) => ({
  data: { overdue, dueThisWeek, upcoming: 1 },
  isLoading: false,
  isError,
})

describe('roleCounts', () => {
  it('lists only the roles held, in role order', () => {
    // Arrange
    const data = summary()

    // Act
    const roles = roleCounts(data)

    // Assert
    expect(roles).toEqual([
      { label: 'Sponsor', count: 1 },
      { label: 'PM', count: 3 },
      { label: 'Task Assignee', count: 4 },
    ])
  })
})

beforeEach(() => {
  mockHome.context = undefined
})

describe('MyProjectsWidget', () => {
  beforeEach(() => {
    vi.clearAllMocks()
  })

  it('skips the request when the home context says there are no projects', () => {
    // Arrange
    mockHome.context = homeContext()
    mockSummary.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: false,
    })

    // Act
    render(<MyProjectsWidget title="My projects" />)

    // Assert
    expect(mockSummary).toHaveBeenCalledWith(expect.anything(), { skip: true })
    expect(
      screen.getByText(/You have no approved or active projects/),
    ).toBeInTheDocument()
  })

  it('shows the project count and a tag per role held, linking to the dashboard', () => {
    // Arrange
    mockSummary.mockReturnValue(loaded(summary()))

    // Act
    render(<MyProjectsWidget title="My projects" />)

    // Assert
    expect(screen.getByText('7')).toBeInTheDocument()
    expect(screen.getByText('approved and active projects')).toBeInTheDocument()
    expect(screen.getByText(/^Sponsor/)).toBeInTheDocument()
    expect(screen.getByText(/^Task Assignee/)).toBeInTheDocument()
    expect(screen.queryByText(/^Owner/)).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'View all' })).toHaveAttribute(
      'href',
      '/ppm/dashboards/projects',
    )
    expect(mockSummary).toHaveBeenCalledWith(
      { status: [ProjectStatus.Approved, ProjectStatus.Active] },
      { skip: false },
    )
  })

  it('says "project" for a count of one', () => {
    // Arrange
    mockSummary.mockReturnValue(loaded(summary({ totalCount: 1 })))

    // Act
    render(<MyProjectsWidget title="My projects" />)

    // Assert
    expect(screen.getByText('approved and active project')).toBeInTheDocument()
  })

  it('says how it fills when the caller has no projects', () => {
    // Arrange
    mockSummary.mockReturnValue(loaded(summary({ totalCount: 0 })))

    // Act
    render(<MyProjectsWidget title="My projects" />)

    // Assert
    expect(screen.getByText('My projects')).toBeInTheDocument()
    expect(
      screen.getByText(/You have no approved or active projects/),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('link', { name: 'Browse projects' }),
    ).toHaveAttribute('href', '/ppm/projects')
  })

  it('draws a skeleton while loading', () => {
    // Arrange
    mockSummary.mockReturnValue({
      data: undefined,
      isLoading: true,
      isError: false,
    })

    // Act
    const { container } = render(<MyProjectsWidget title="My projects" />)

    // Assert
    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
    expect(screen.queryByText(/You have no/)).not.toBeInTheDocument()
  })

  it('says the projects failed to load rather than that there are none', () => {
    // Arrange
    mockSummary.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    })

    // Act
    render(<MyProjectsWidget title="My projects" />)

    // Assert
    expect(
      screen.getByText('Could not load your projects.'),
    ).toBeInTheDocument()
    expect(screen.queryByText(/You have no/)).not.toBeInTheDocument()
  })
})

describe('OverdueTasksTile', () => {
  it('counts zero without asking when the home context says there are no projects', () => {
    // Arrange
    mockHome.context = homeContext()
    mockMetrics.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: false,
    })

    // Act
    render(<OverdueTasksTile />)

    // Assert
    expect(mockMetrics).toHaveBeenCalledWith(expect.anything(), { skip: true })
    expect(
      screen.getByRole('button', { name: 'Overdue tasks on your projects: 0' }),
    ).toBeInTheDocument()
  })

  it('counts overdue tasks in the error colour', () => {
    // Arrange
    mockMetrics.mockReturnValue(metrics(7, 2))

    // Act
    const { container } = render(<OverdueTasksTile />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Overdue tasks on your projects: 7' }),
    ).toBeInTheDocument()
    expect(tileValueStyle(container)).toContain('var(--ant-color-error)')
  })

  it('leaves no overdue tasks uncoloured', () => {
    // Arrange
    mockMetrics.mockReturnValue(metrics(0, 2))

    // Act
    const { container } = render(<OverdueTasksTile />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Overdue tasks on your projects: 0' }),
    ).toBeInTheDocument()
    expect(tileValueStyle(container)).not.toContain('var(--ant-color')
  })

  it('shows a dash, not the last good count, after a failed refetch', () => {
    // Arrange
    mockMetrics.mockReturnValue(metrics(7, 2, true))

    // Act
    render(<OverdueTasksTile />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Overdue tasks on your projects: unavailable',
      }),
    ).toBeInTheDocument()
  })
})

describe('DueThisWeekTile', () => {
  it('counts tasks due this week in the warning colour', () => {
    // Arrange
    mockMetrics.mockReturnValue(metrics(7, 2))

    // Act
    const { container } = render(<DueThisWeekTile />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Due this week on your projects: 2' }),
    ).toBeInTheDocument()
    expect(tileValueStyle(container)).toContain('var(--ant-color-warning)')
  })

  it('leaves nothing due uncoloured', () => {
    // Arrange
    mockMetrics.mockReturnValue(metrics(7, 0))

    // Act
    const { container } = render(<DueThisWeekTile />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Due this week on your projects: 0' }),
    ).toBeInTheDocument()
    expect(tileValueStyle(container)).not.toContain('var(--ant-color')
  })

  it('shows a dash, not the last good count, after a failed refetch', () => {
    // Arrange
    mockMetrics.mockReturnValue(metrics(7, 2, true))

    // Act
    render(<DueThisWeekTile />)

    // Assert
    expect(
      screen.getByRole('button', {
        name: 'Due this week on your projects: unavailable',
      }),
    ).toBeInTheDocument()
  })
})
