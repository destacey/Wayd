import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { EmployeeNavigationDto } from '@/src/services/wayd-api'
import RecordRoleList from './record-role-list'

const mockEmployee = jest.fn()

jest.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetEmployeeByIdQuery: (...args: unknown[]) => mockEmployee(...args),
}))

const person = (
  id: string,
  key: number,
  name: string,
): EmployeeNavigationDto => ({ id, key, name })

const DANA = person('11111111-0000-4000-a000-000000000001', 1042, 'Dana Reid')
const SAM = person('22222222-0000-4000-a000-000000000002', 1043, 'Sam Okafor')

describe('RecordRoleList', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockEmployee.mockReturnValue({
      data: {
        id: DANA.id,
        key: DANA.key,
        displayName: DANA.name,
        jobTitle: 'Delivery Lead',
      },
      isLoading: false,
      isError: false,
    })
  })

  it('shows the empty text when nobody holds the role', () => {
    // Arrange / Act
    render(<RecordRoleList people={[]} emptyText="No owner assigned" />)

    // Assert
    expect(screen.getByText('No owner assigned')).toBeInTheDocument()
  })

  it('links each person to their employee record', () => {
    // Arrange / Act
    render(<RecordRoleList people={[DANA]} emptyText="No owner assigned" />)

    // Assert
    expect(screen.getByRole('link', { name: 'Dana Reid' })).toHaveAttribute(
      'href',
      '/organizations/employees/1042',
    )
  })

  it('sorts people case-insensitively rather than by arrival order', () => {
    // Arrange
    const zara = person('33333333-0000-4000-a000-000000000003', 1044, 'zara Ito')
    const alex = person('44444444-0000-4000-a000-000000000004', 1045, 'Alex Bell')

    // Act
    render(
      <RecordRoleList people={[zara, alex, DANA]} emptyText="None" />,
    )

    // Assert
    const names = screen
      .getAllByRole('link')
      .map((link) => link.textContent)
    expect(names).toEqual(['Alex Bell', 'Dana Reid', 'zara Ito'])
  })

  it('does not fetch employee records until a card is opened', () => {
    // Arrange / Act — a project can carry a long member list, so the records
    // are only requested for the person actually asked about.
    render(<RecordRoleList people={[DANA, SAM]} emptyText="None" />)

    // Assert
    expect(mockEmployee).not.toHaveBeenCalled()
  })

  it('opens the person card from the avatar, keyed by employee id', async () => {
    // Arrange
    const user = userEvent.setup()
    render(<RecordRoleList people={[DANA]} emptyText="None" />)

    // Act — the avatar carries the initials, so it is what gets clicked.
    await user.click(screen.getByText('DR'))

    // Assert
    expect(mockEmployee).toHaveBeenCalledWith(DANA.id)
    expect(await screen.findByText('Delivery Lead')).toBeInTheDocument()
  })
})
