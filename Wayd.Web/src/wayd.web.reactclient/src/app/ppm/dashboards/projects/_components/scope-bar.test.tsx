import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import ScopeBar from './scope-bar'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

jest.mock('@/src/store/features/ppm/projects-api', () => ({
  useGetProjectStatusOptionsQuery: jest.fn(),
}))

jest.mock('@/src/store/features/ppm/portfolios-api', () => ({
  useGetPortfolioOptionsQuery: jest.fn(),
}))

jest.mock('@/src/store/features/ppm/programs-api', () => ({
  useGetProgramOptionsQuery: jest.fn(),
}))

jest.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeeOptionsQuery: jest.fn(),
}))

import { useGetProjectStatusOptionsQuery } from '@/src/store/features/ppm/projects-api'
import { useGetPortfolioOptionsQuery } from '@/src/store/features/ppm/portfolios-api'
import { useGetProgramOptionsQuery } from '@/src/store/features/ppm/programs-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'

const mockStatusQuery = useGetProjectStatusOptionsQuery as jest.Mock
const mockEmployeeQuery = useGetEmployeeOptionsQuery as jest.Mock
const mockPortfolioQuery = useGetPortfolioOptionsQuery as jest.Mock
const mockProgramQuery = useGetProgramOptionsQuery as jest.Mock

const defaultProps = {
  scope: { kind: 'me' } as const,
  onScopeChange: jest.fn(),
  hasLinkedEmployee: true,
  selectedRoles: [] as number[],
  onRoleChange: jest.fn(),
  selectedStatuses: [] as number[],
  onStatusChange: jest.fn(),
  onReset: jest.fn(),
  onRefresh: jest.fn(),
}

describe('ScopeBar', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockStatusQuery.mockReturnValue({
      data: [
        { value: 1, label: 'Proposed', lifecycleCategory: 'NotStarted' },
        { value: 2, label: 'Active', lifecycleCategory: 'Active' },
      ],
      isLoading: false,
    })
    mockEmployeeQuery.mockReturnValue({
      data: [{ label: 'Ada Lovelace', value: 'emp-ada' }],
      isLoading: false,
    })
    mockPortfolioQuery.mockReturnValue({
      data: [{ label: 'Customer Platform', value: 'port-1' }],
      isLoading: false,
    })
    mockProgramQuery.mockReturnValue({
      data: [{ label: 'Payments', value: 'prog-1' }],
      isLoading: false,
    })
  })

  it('switches scope kind with nothing chosen yet', async () => {
    // Arrange
    render(<ScopeBar {...defaultProps} />)

    // Act
    await userEvent.click(screen.getByText('Person'))
    await userEvent.click(screen.getByText('Portfolio'))
    await userEvent.click(screen.getByText('All projects'))

    // Assert
    expect(defaultProps.onScopeChange).toHaveBeenNthCalledWith(1, {
      kind: 'person',
      employeeId: null,
    })
    expect(defaultProps.onScopeChange).toHaveBeenNthCalledWith(2, {
      kind: 'portfolio',
      portfolioId: null,
    })
    expect(defaultProps.onScopeChange).toHaveBeenNthCalledWith(3, {
      kind: 'all',
    })
  })

  it('shows the picker that matches the scope kind', () => {
    // Arrange / Act
    const { rerender } = render(<ScopeBar {...defaultProps} />)

    // Assert
    expect(screen.queryByText('Choose an employee')).not.toBeInTheDocument()

    rerender(
      <ScopeBar
        {...defaultProps}
        scope={{ kind: 'person', employeeId: null }}
      />,
    )
    expect(screen.getByText('Choose an employee')).toBeInTheDocument()

    rerender(
      <ScopeBar
        {...defaultProps}
        scope={{ kind: 'portfolio', portfolioId: null }}
      />,
    )
    expect(screen.getByText('Choose a portfolio')).toBeInTheDocument()

    rerender(
      <ScopeBar
        {...defaultProps}
        scope={{ kind: 'program', programId: null }}
      />,
    )
    expect(screen.getByText('Choose a program')).toBeInTheDocument()
  })

  it('picks a portfolio from its options', async () => {
    // Arrange
    render(
      <ScopeBar
        {...defaultProps}
        scope={{ kind: 'portfolio', portfolioId: null }}
      />,
    )

    // Act
    await userEvent.click(
      screen.getByRole('combobox', { name: 'Choose a portfolio' }),
    )
    await userEvent.click(await screen.findByText('Customer Platform'))

    // Assert
    expect(defaultProps.onScopeChange).toHaveBeenCalledWith({
      kind: 'portfolio',
      portfolioId: 'port-1',
    })
  })

  it('offers no Me option to an account without a linked employee', () => {
    // Arrange / Act
    render(
      <ScopeBar
        {...defaultProps}
        hasLinkedEmployee={false}
        scope={{ kind: 'person', employeeId: null }}
      />,
    )

    // Assert
    expect(screen.queryByText('Me')).not.toBeInTheDocument()
    expect(screen.getByText('Person')).toBeInTheDocument()
  })

  it('shows role chips only for a person scope', () => {
    // Arrange / Act
    const { rerender } = render(<ScopeBar {...defaultProps} />)

    // Assert
    expect(screen.getByRole('button', { name: 'Sponsor' })).toBeInTheDocument()

    rerender(<ScopeBar {...defaultProps} scope={{ kind: 'all' }} />)
    expect(
      screen.queryByRole('button', { name: 'Sponsor' }),
    ).not.toBeInTheDocument()
    // Status chips stay in every scope
    expect(screen.getByRole('button', { name: 'Active' })).toBeInTheDocument()
  })

  it('toggles a role chip and clears roles with All', async () => {
    // Arrange
    render(<ScopeBar {...defaultProps} selectedRoles={[2]} />)

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'PM' }))
    await userEvent.click(screen.getByRole('button', { name: 'All' }))

    // Assert
    expect(defaultProps.onRoleChange).toHaveBeenNthCalledWith(1, [2, 3])
    expect(defaultProps.onRoleChange).toHaveBeenNthCalledWith(2, [])
  })

  it('dashes the unlit status buttons and colors only the lit ones', () => {
    // Arrange / Act
    render(<ScopeBar {...defaultProps} selectedStatuses={[2]} />)

    // Assert — the dash, not the color, carries selection
    const active = screen.getByRole('button', { name: 'Active' })
    const proposed = screen.getByRole('button', { name: 'Proposed' })
    expect(active.style.borderStyle).toBe('')
    expect(active.style.backgroundColor).not.toBe('')
    expect(proposed.style.borderStyle).toBe('dashed')
  })

  it('renders a skeleton while status options load', () => {
    // Arrange
    mockStatusQuery.mockReturnValue({ data: undefined, isLoading: true })

    // Act
    const { container } = render(<ScopeBar {...defaultProps} />)

    // Assert
    expect(container.querySelector('.ant-skeleton')).toBeInTheDocument()
  })
})
