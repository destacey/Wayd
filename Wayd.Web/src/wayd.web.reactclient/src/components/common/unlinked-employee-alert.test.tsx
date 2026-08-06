import { render, screen } from '@testing-library/react'
import UnlinkedEmployeeAlert from './unlinked-employee-alert'

const mockLink = { hasLinkedEmployee: true }

jest.mock('@/src/hooks', () => ({
  useLinkedEmployee: () => ({
    employeeId: mockLink.hasLinkedEmployee ? 'emp-1' : null,
    hasLinkedEmployee: mockLink.hasLinkedEmployee,
  }),
}))

describe('UnlinkedEmployeeAlert', () => {
  beforeEach(() => {
    mockLink.hasLinkedEmployee = true
  })

  it('renders nothing when the account is linked', () => {
    render(<UnlinkedEmployeeAlert consequence="Nothing to show." />)

    expect(
      screen.queryByText("Your account isn't linked to an employee record"),
    ).not.toBeInTheDocument()
  })

  it('explains the cause and the consequence when the account is unlinked', () => {
    // Without this the page just renders empty, which reads as a broken dashboard rather than an
    // account that needs linking.
    mockLink.hasLinkedEmployee = false

    render(
      <UnlinkedEmployeeAlert consequence="Projects are assigned to employees, so this dashboard has nothing to show." />,
    )

    expect(
      screen.getByText("Your account isn't linked to an employee record"),
    ).toBeInTheDocument()
    expect(screen.getByText(/nothing to show/)).toBeInTheDocument()
    expect(screen.getByText(/Ask an administrator/)).toBeInTheDocument()
  })

  it('stays hidden when the caller suppresses it', () => {
    // `when` lets a page warn only the users for whom the link actually matters.
    mockLink.hasLinkedEmployee = false

    render(<UnlinkedEmployeeAlert consequence="Nothing to show." when={false} />)

    expect(
      screen.queryByText("Your account isn't linked to an employee record"),
    ).not.toBeInTheDocument()
  })
})
