import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import PersonPopover from './person-popover'

const mockEmployee = jest.fn()

jest.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeeByIdQuery: (...args: unknown[]) => mockEmployee(...args),
}))

const EMPLOYEE_ID = '8f2c1b40-0000-4000-a000-000000000001'

const employee = {
  id: EMPLOYEE_ID,
  key: 1042,
  displayName: 'Priya Raghunathan',
  email: 'priya.raghunathan@acme.example',
  jobTitle: 'Principal Engineer',
  department: 'Platform Engineering',
  officeLocation: 'Austin, TX',
} as any

describe('PersonPopover', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockEmployee.mockReturnValue({
      data: employee,
      isLoading: false,
      isError: false,
    })
  })

  it('shows the person on demand rather than fetching for every avatar', () => {
    // Arrange / Act — a board can carry dozens of these, so the record is only
    // requested once a card is opened.
    render(<PersonPopover name="Priya Raghunathan" employeeId={EMPLOYEE_ID} />)

    // Assert
    expect(mockEmployee).not.toHaveBeenCalled()
  })

  it('opens the card on click', async () => {
    // Arrange
    const user = userEvent.setup()
    render(<PersonPopover name="Priya Raghunathan" employeeId={EMPLOYEE_ID} />)

    // Act
    await user.click(screen.getByText('PR'))

    // Assert
    expect(await screen.findByText('Principal Engineer')).toBeInTheDocument()
    expect(screen.getByText('Platform Engineering')).toBeInTheDocument()
  })

  it('links through to the full employee record', async () => {
    // Arrange
    const user = userEvent.setup()
    render(<PersonPopover name="Priya Raghunathan" employeeId={EMPLOYEE_ID} />)

    // Act
    await user.click(screen.getByText('PR'))

    // Assert — the key, not the id: that is what the route resolves on.
    expect(
      await screen.findByRole('link', { name: 'Priya Raghunathan' }),
    ).toHaveAttribute('href', '/organizations/employees/1042')
  })

  it('omits a field the employee has no value for', async () => {
    // Arrange
    mockEmployee.mockReturnValue({
      data: { ...employee, department: undefined, officeLocation: undefined },
      isLoading: false,
      isError: false,
    })
    const user = userEvent.setup()
    render(<PersonPopover name="Priya Raghunathan" employeeId={EMPLOYEE_ID} />)

    // Act
    await user.click(screen.getByText('PR'))

    // Assert
    expect(await screen.findByText('Principal Engineer')).toBeInTheDocument()
    expect(screen.queryByText('Department')).toBeNull()
    expect(screen.queryByText('Office')).toBeNull()
  })

  it('renders a plain avatar when the account has no employee record', async () => {
    // Arrange / Act — an unlinked account has nothing to show, so the click
    // affordance is dropped rather than opening an empty card.
    const user = userEvent.setup()
    render(<PersonPopover name="Priya Raghunathan" />)

    // Assert
    await user.click(screen.getByText('PR'))
    expect(screen.queryByText('Principal Engineer')).toBeNull()
    expect(mockEmployee).not.toHaveBeenCalled()
  })

  it('still names the person when the record cannot be loaded', async () => {
    // Arrange
    mockEmployee.mockReturnValue({
      data: undefined,
      isLoading: false,
      isError: true,
    })
    const user = userEvent.setup()
    render(<PersonPopover name="Priya Raghunathan" employeeId={EMPLOYEE_ID} />)

    // Act
    await user.click(screen.getByText('PR'))

    // Assert — the name came from the caller and is still worth showing.
    expect(await screen.findByText('Priya Raghunathan')).toBeInTheDocument()
    expect(screen.getByText('Details are unavailable.')).toBeInTheDocument()
  })

  it('does not trigger a clickable container it sits on', async () => {
    // Arrange — these avatars sit on project cards that navigate on click, so
    // opening the card must not also open the project.
    const onContainerClick = jest.fn()
    const user = userEvent.setup()
    render(
      <div onClick={onContainerClick}>
        <PersonPopover name="Priya Raghunathan" employeeId={EMPLOYEE_ID} />
      </div>,
    )

    // Act
    await user.click(screen.getByText('PR'))

    // Assert
    expect(await screen.findByText('Principal Engineer')).toBeInTheDocument()
    expect(onContainerClick).not.toHaveBeenCalled()
  })
})
