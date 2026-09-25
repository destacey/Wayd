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

jest.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeeOptionsQuery: jest.fn(),
}))

import { useGetProjectStatusOptionsQuery } from '@/src/store/features/ppm/projects-api'
import { useGetEmployeeOptionsQuery } from '@/src/store/features/organizations/employee-api'

const mockStatusQuery = useGetProjectStatusOptionsQuery as jest.Mock
const mockEmployeeQuery = useGetEmployeeOptionsQuery as jest.Mock

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
  })

  it('switches to person scope with no employee chosen yet', async () => {
    // Arrange
    render(<ScopeBar {...defaultProps} />)

    // Act
    await userEvent.click(screen.getByText('Person'))

    // Assert
    expect(defaultProps.onScopeChange).toHaveBeenCalledWith({
      kind: 'person',
      employeeId: null,
    })
  })

  it('shows the employee picker only in person scope', () => {
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
