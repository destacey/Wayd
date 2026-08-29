import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import MyProjectsCard from './my-projects-card'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

const mockPush = jest.fn()

jest.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}))

const mockHasPermissionClaim = jest.fn()
const mockAuth = { employeeId: 'emp-1' as string | null }

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({
    user: { employeeId: mockAuth.employeeId },
    hasClaim: () => false,
    hasPermissionClaim: mockHasPermissionClaim,
  }),
}))

jest.mock('@/src/hooks', () => ({
  useLinkedEmployee: () => ({
    employeeId: mockAuth.employeeId,
    hasLinkedEmployee: mockAuth.employeeId !== null,
  }),
}))

jest.mock('@/src/store/features/ppm/projects-api', () => ({
  useGetMyProjectsSummaryQuery: jest.fn(),
}))

import { useGetMyProjectsSummaryQuery } from '@/src/store/features/ppm/projects-api'

const mockQuery = useGetMyProjectsSummaryQuery as jest.Mock

describe('MyProjectsCard', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockHasPermissionClaim.mockReturnValue(true)
    mockAuth.employeeId = 'emp-1'
  })

  it('renders nothing and skips the request when the account has no linked employee', () => {
    // Project roles are held by employees, so the answer is known to be empty — no round-trip.
    mockAuth.employeeId = null
    mockQuery.mockReturnValue({ data: undefined, isLoading: false })

    const { container } = render(<MyProjectsCard />)

    expect(container).toBeEmptyDOMElement()
    expect(mockQuery).toHaveBeenCalledWith(
      expect.anything(),
      expect.objectContaining({ skip: true }),
    )
  })

  it('renders nothing when user lacks permission', () => {
    mockHasPermissionClaim.mockReturnValue(false)
    mockQuery.mockReturnValue({ data: undefined, isLoading: false })

    const { container } = render(<MyProjectsCard />)

    expect(container).toBeEmptyDOMElement()
  })

  it('renders nothing when no projects', () => {
    mockQuery.mockReturnValue({
      data: { totalCount: 0, sponsorCount: 0, ownerCount: 0, managerCount: 0, memberCount: 0, assigneeCount: 0 },
      isLoading: false,
    })

    const { container } = render(<MyProjectsCard />)

    expect(container).toBeEmptyDOMElement()
  })

  it('renders loading skeleton when loading', () => {
    mockQuery.mockReturnValue({ data: undefined, isLoading: true })

    const { container } = render(<MyProjectsCard />)

    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
  })

  it('renders project count', () => {
    mockQuery.mockReturnValue({
      data: { totalCount: 7, sponsorCount: 2, ownerCount: 1, managerCount: 2, memberCount: 1, assigneeCount: 1 },
      isLoading: false,
    })

    render(<MyProjectsCard />)

    const heading = screen.getByRole('heading', { level: 3 })
    expect(heading).toHaveTextContent('7')
    expect(screen.getByText('projects')).toBeInTheDocument()
  })

  it('renders singular "project" for count of 1', () => {
    mockQuery.mockReturnValue({
      data: { totalCount: 1, sponsorCount: 0, ownerCount: 1, managerCount: 0, memberCount: 0, assigneeCount: 0 },
      isLoading: false,
    })

    render(<MyProjectsCard />)

    expect(screen.getByText('project')).toBeInTheDocument()
  })

  it('renders role chips only for roles with count > 0', () => {
    mockQuery.mockReturnValue({
      data: { totalCount: 5, sponsorCount: 2, ownerCount: 0, managerCount: 3, memberCount: 0, assigneeCount: 0 },
      isLoading: false,
    })

    render(<MyProjectsCard />)

    expect(screen.getByText('Sponsor')).toBeInTheDocument()
    expect(screen.getByText('PM')).toBeInTheDocument()
    expect(screen.queryByText('Owner')).not.toBeInTheDocument()
    expect(screen.queryByText('Member')).not.toBeInTheDocument()
    expect(screen.queryByText('Task Assignee')).not.toBeInTheDocument()
  })

  it('renders Task Assignee chip when assigneeCount > 0', () => {
    mockQuery.mockReturnValue({
      data: { totalCount: 1, sponsorCount: 0, ownerCount: 0, managerCount: 0, memberCount: 0, assigneeCount: 1 },
      isLoading: false,
    })

    render(<MyProjectsCard />)

    expect(screen.getByText('Task Assignee')).toBeInTheDocument()
  })

  it('renders "View all" label', () => {
    mockQuery.mockReturnValue({
      data: { totalCount: 1, sponsorCount: 0, ownerCount: 0, managerCount: 0, memberCount: 0, assigneeCount: 1 },
      isLoading: false,
    })

    render(<MyProjectsCard />)

    expect(screen.getByText(/View all/)).toBeInTheDocument()
  })

  it('navigates to My Projects dashboard on click', async () => {
    mockQuery.mockReturnValue({
      data: { totalCount: 1, sponsorCount: 0, ownerCount: 0, managerCount: 0, memberCount: 0, assigneeCount: 1 },
      isLoading: false,
    })

    render(<MyProjectsCard />)

    await userEvent.click(screen.getByText(/My Projects/i))

    expect(mockPush).toHaveBeenCalledWith('/ppm/dashboards/my-projects')
  })

  it('skips API call when user lacks permission', () => {
    mockHasPermissionClaim.mockReturnValue(false)
    mockQuery.mockReturnValue({ data: undefined, isLoading: false })

    render(<MyProjectsCard />)

    expect(mockQuery).toHaveBeenCalledWith({ status: [5, 2] }, { skip: true })
  })
})
