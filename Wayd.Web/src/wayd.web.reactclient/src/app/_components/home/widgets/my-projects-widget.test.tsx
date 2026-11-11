import type { Mock } from 'vitest'
import { render, screen } from '@testing-library/react'
import MyProjectsWidget, { roleCounts } from './my-projects-widget'
import {
  HomeContextResponse,
  MyProjectsSummaryDto,
  ProjectStatus,
} from '@/src/services/wayd-api'
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
}))

import { useGetMyProjectsSummaryQuery } from '@/src/store/features/ppm/projects-api'

const mockSummary = useGetMyProjectsSummaryQuery as Mock

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
