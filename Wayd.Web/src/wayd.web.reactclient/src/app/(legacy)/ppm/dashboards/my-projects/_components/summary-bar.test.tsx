import { render, screen } from '@testing-library/react'
import MyProjectsSummaryBar from './summary-bar'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

jest.mock('@/src/components/contexts/theme', () => ({
  __esModule: true,
  default: () => ({
    token: {
      colorError: '#ff4d4f',
      colorWarning: '#faad14',
    },
  }),
}))

jest.mock('@/src/store/features/ppm/projects-api', () => ({
  useGetMyProjectsTaskMetricsQuery: jest.fn(),
}))

import { useGetMyProjectsTaskMetricsQuery } from '@/src/store/features/ppm/projects-api'

const mockQuery = useGetMyProjectsTaskMetricsQuery as jest.Mock

describe('MyProjectsSummaryBar', () => {
  beforeEach(() => {
    jest.clearAllMocks()
  })

  it('renders loading skeleton when isLoading is true', () => {
    mockQuery.mockReturnValue({ data: undefined, isLoading: true })

    const { container } = render(
      <MyProjectsSummaryBar
        projectCount={0}
        selectedStatuses={[]}
        selectedRoles={[]}
        isLoading={true}
      />,
    )

    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
  })

  it('renders project count', () => {
    mockQuery.mockReturnValue({
      data: { overdue: 0, dueThisWeek: 0, upcoming: 0 },
      isLoading: false,
    })

    render(
      <MyProjectsSummaryBar
        projectCount={5}
        selectedStatuses={[]}
        selectedRoles={[]}
        isLoading={false}
      />,
    )

    expect(screen.getByText('Projects')).toBeInTheDocument()
    expect(screen.getByText('5')).toBeInTheDocument()
  })

  it('renders zero project count', () => {
    mockQuery.mockReturnValue({
      data: { overdue: 0, dueThisWeek: 0, upcoming: 0 },
      isLoading: false,
    })

    render(
      <MyProjectsSummaryBar
        projectCount={0}
        selectedStatuses={[]}
        selectedRoles={[]}
        isLoading={false}
      />,
    )

    expect(screen.getByText('Projects')).toBeInTheDocument()
    const statValues = document.querySelectorAll(
      '.ant-statistic-content-value',
    )
    const projectValue = Array.from(statValues).find(
      (el) => el.textContent === '0',
    )
    expect(projectValue).toBeTruthy()
  })

  it('renders overdue count from query', () => {
    mockQuery.mockReturnValue({
      data: { overdue: 5, dueThisWeek: 0, upcoming: 0 },
      isLoading: false,
    })

    render(
      <MyProjectsSummaryBar
        projectCount={3}
        selectedStatuses={[]}
        selectedRoles={[]}
        isLoading={false}
      />,
    )

    expect(screen.getByText('5')).toBeInTheDocument()
  })

  it('renders dueThisWeek count from query', () => {
    mockQuery.mockReturnValue({
      data: { overdue: 0, dueThisWeek: 4, upcoming: 0 },
      isLoading: false,
    })

    render(
      <MyProjectsSummaryBar
        projectCount={1}
        selectedStatuses={[]}
        selectedRoles={[]}
        isLoading={false}
      />,
    )

    expect(screen.getByText('4')).toBeInTheDocument()
  })

  it('renders upcoming count from query', () => {
    mockQuery.mockReturnValue({
      data: { overdue: 0, dueThisWeek: 0, upcoming: 7 },
      isLoading: false,
    })

    render(
      <MyProjectsSummaryBar
        projectCount={1}
        selectedStatuses={[]}
        selectedRoles={[]}
        isLoading={false}
      />,
    )

    expect(screen.getByText('7')).toBeInTheDocument()
  })

  it('shows dash placeholders when metrics are loading', () => {
    mockQuery.mockReturnValue({ data: undefined, isLoading: true })

    render(
      <MyProjectsSummaryBar
        projectCount={2}
        selectedStatuses={[]}
        selectedRoles={[]}
        isLoading={false}
      />,
    )

    const dashes = screen.getAllByText('-')
    expect(dashes).toHaveLength(3)
  })

  it('renders all metric titles', () => {
    mockQuery.mockReturnValue({
      data: { overdue: 0, dueThisWeek: 0, upcoming: 0 },
      isLoading: false,
    })

    render(
      <MyProjectsSummaryBar
        projectCount={1}
        selectedStatuses={[]}
        selectedRoles={[]}
        isLoading={false}
      />,
    )

    expect(screen.getByText('Projects')).toBeInTheDocument()
    expect(screen.getByText('Overdue')).toBeInTheDocument()
    expect(screen.getByText('Due This Week')).toBeInTheDocument()
    expect(screen.getByText('Upcoming')).toBeInTheDocument()
  })

  it('passes status filter to the query', () => {
    mockQuery.mockReturnValue({
      data: { overdue: 0, dueThisWeek: 0, upcoming: 0 },
      isLoading: false,
    })

    render(
      <MyProjectsSummaryBar
        projectCount={1}
        selectedStatuses={[2, 5]}
        selectedRoles={[]}
        isLoading={false}
      />,
    )

    expect(mockQuery).toHaveBeenCalledWith({ status: [2, 5] })
  })

  it('passes undefined to query when no statuses selected', () => {
    mockQuery.mockReturnValue({
      data: { overdue: 0, dueThisWeek: 0, upcoming: 0 },
      isLoading: false,
    })

    render(
      <MyProjectsSummaryBar
        projectCount={1}
        selectedStatuses={[]}
        selectedRoles={[]}
        isLoading={false}
      />,
    )

    expect(mockQuery).toHaveBeenCalledWith(undefined)
  })

  it('defaults to zero when metrics data is undefined', () => {
    mockQuery.mockReturnValue({ data: undefined, isLoading: false })

    render(
      <MyProjectsSummaryBar
        projectCount={1}
        selectedStatuses={[]}
        selectedRoles={[]}
        isLoading={false}
      />,
    )

    const statValues = document.querySelectorAll(
      '.ant-statistic-content-value',
    )
    const zeros = Array.from(statValues).filter(
      (el) => el.textContent === '0',
    )
    // Projects (0 wouldn't be here since projectCount=1), Overdue, DueThisWeek, Upcoming
    expect(zeros.length).toBeGreaterThanOrEqual(3)
  })
})
