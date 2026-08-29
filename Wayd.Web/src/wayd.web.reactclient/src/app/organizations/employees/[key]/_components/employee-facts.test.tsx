import { render, screen } from '@testing-library/react'
import EmployeeFacts from './employee-facts'

const mockDirectReports = jest.fn()

jest.mock('@/src/store/features/organizations/employee-api', () => ({
  useGetDirectReportsQuery: (...args: unknown[]) => mockDirectReports(...args),
}))

const employee = {
  id: 'employee-1',
  key: 1042,
  displayName: 'Priya Raghunathan',
  email: 'priya.raghunathan@acme.example',
  employeeNumber: 'E-10428',
  isActive: true,
  emails: [],
} as any

const report = (id: string, key: number, displayName: string) => ({
  id,
  key,
  displayName,
})

describe('EmployeeFacts', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockDirectReports.mockReturnValue({ data: [], isLoading: false })
  })

  it('always shows the manager, including when there is none', () => {
    // Arrange / Act
    render(<EmployeeFacts employee={employee} />)

    // Assert
    expect(screen.getByText('Manager')).toBeInTheDocument()
    expect(screen.getByText('No manager assigned')).toBeInTheDocument()
  })

  it('links to the manager when there is one', () => {
    // Arrange / Act
    render(
      <EmployeeFacts
        employee={{
          ...employee,
          manager: { id: 'm1', key: 77, name: 'Daniel Okonkwo' },
        }}
      />,
    )

    // Assert
    expect(screen.getByRole('link', { name: /Daniel Okonkwo/ })).toHaveAttribute(
      'href',
      '/organizations/employees/77',
    )
  })

  it('omits direct reports entirely when there are none', () => {
    // Arrange / Act — most people have none, so an empty row would be noise.
    render(<EmployeeFacts employee={employee} />)

    // Assert
    expect(screen.queryByText('Direct Reports')).toBeNull()
  })

  it('lists direct reports in name order', () => {
    // Arrange
    mockDirectReports.mockReturnValue({
      data: [
        report('r1', 11, 'wei chen'),
        report('r2', 12, 'Amara Sithole'),
      ],
      isLoading: false,
    })

    // Act
    render(<EmployeeFacts employee={employee} />)

    // Assert — case-insensitive, so a lowercase name does not sort to the end
    expect(screen.getByText('Direct Reports')).toBeInTheDocument()
    const names = screen.getAllByRole('link').map((l) => l.textContent)
    expect(names.indexOf('Amara Sithole')).toBeLessThan(names.indexOf('wei chen'))
  })

  it('does not repeat the job title the header already carries', () => {
    // Arrange / Act
    render(
      <EmployeeFacts
        employee={{ ...employee, jobTitle: 'Principal Engineer' }}
      />,
    )

    // Assert
    expect(screen.queryByText('Job Title')).toBeNull()
  })
})
