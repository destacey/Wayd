import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import MyProjectsDashboardFilterBar from './filter-bar'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

jest.mock('@/src/store/features/ppm/projects-api', () => ({
  useGetProjectStatusOptionsQuery: jest.fn(),
}))

import { useGetProjectStatusOptionsQuery } from '@/src/store/features/ppm/projects-api'

const mockStatusQuery = useGetProjectStatusOptionsQuery as jest.Mock

const defaultProps = {
  selectedRoles: [] as number[],
  onRoleChange: jest.fn(),
  selectedStatuses: [] as number[],
  onStatusChange: jest.fn(),
  onReset: jest.fn(),
  onRefresh: jest.fn(),
}

describe('MyProjectsDashboardFilterBar', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockStatusQuery.mockReturnValue({
      data: [
        { value: 1, label: 'Proposed' },
        { value: 2, label: 'Active' },
        { value: 3, label: 'Completed' },
      ],
      isLoading: false,
    })
  })

  it('renders loading skeleton when status options are loading', () => {
    mockStatusQuery.mockReturnValue({ data: undefined, isLoading: true })

    const { container } = render(
      <MyProjectsDashboardFilterBar {...defaultProps} />,
    )

    expect(
      container.querySelector('.ant-skeleton'),
    ).toBeInTheDocument()
  })

  it('renders role filter chips', () => {
    render(<MyProjectsDashboardFilterBar {...defaultProps} />)

    expect(screen.getByText('All')).toBeInTheDocument()
    expect(screen.getByText('Sponsor')).toBeInTheDocument()
    expect(screen.getByText('Owner')).toBeInTheDocument()
    expect(screen.getByText('PM')).toBeInTheDocument()
    expect(screen.getByText('Member')).toBeInTheDocument()
    expect(screen.getByText('Task Assignee')).toBeInTheDocument()
  })

  it('renders status filter chips from API data', () => {
    render(<MyProjectsDashboardFilterBar {...defaultProps} />)

    expect(screen.getByText('Proposed')).toBeInTheDocument()
    expect(screen.getByText('Active')).toBeInTheDocument()
    expect(screen.getByText('Completed')).toBeInTheDocument()
  })

  it('calls onRoleChange with empty array when All is clicked', async () => {
    const onRoleChange = jest.fn()
    render(
      <MyProjectsDashboardFilterBar
        {...defaultProps}
        selectedRoles={[1, 2]}
        onRoleChange={onRoleChange}
      />,
    )

    await userEvent.click(screen.getByText('All'))

    expect(onRoleChange).toHaveBeenCalledWith([])
  })

  it('toggles role on when clicking unselected role', async () => {
    const onRoleChange = jest.fn()
    render(
      <MyProjectsDashboardFilterBar
        {...defaultProps}
        selectedRoles={[]}
        onRoleChange={onRoleChange}
      />,
    )

    await userEvent.click(screen.getByText('Sponsor'))

    expect(onRoleChange).toHaveBeenCalledWith([1])
  })

  it('toggles role off when clicking selected role', async () => {
    const onRoleChange = jest.fn()
    render(
      <MyProjectsDashboardFilterBar
        {...defaultProps}
        selectedRoles={[1, 2]}
        onRoleChange={onRoleChange}
      />,
    )

    await userEvent.click(screen.getByText('Sponsor'))

    expect(onRoleChange).toHaveBeenCalledWith([2])
  })

  it('toggles status on when clicking unselected status', async () => {
    const onStatusChange = jest.fn()
    render(
      <MyProjectsDashboardFilterBar
        {...defaultProps}
        selectedStatuses={[]}
        onStatusChange={onStatusChange}
      />,
    )

    await userEvent.click(screen.getByText('Active'))

    expect(onStatusChange).toHaveBeenCalledWith([2])
  })

  it('toggles status off when clicking selected status', async () => {
    const onStatusChange = jest.fn()
    render(
      <MyProjectsDashboardFilterBar
        {...defaultProps}
        selectedStatuses={[1, 2]}
        onStatusChange={onStatusChange}
      />,
    )

    await userEvent.click(screen.getByText('Active'))

    expect(onStatusChange).toHaveBeenCalledWith([1])
  })

  it('calls onReset when reset button is clicked', async () => {
    const onReset = jest.fn()
    render(
      <MyProjectsDashboardFilterBar {...defaultProps} onReset={onReset} />,
    )

    await userEvent.click(screen.getByLabelText('Reset filters'))

    expect(onReset).toHaveBeenCalledTimes(1)
  })

  it('calls onRefresh when refresh button is clicked', async () => {
    const onRefresh = jest.fn()
    render(
      <MyProjectsDashboardFilterBar
        {...defaultProps}
        onRefresh={onRefresh}
      />,
    )

    await userEvent.click(screen.getByLabelText('Refresh data'))

    expect(onRefresh).toHaveBeenCalledTimes(1)
  })
})
