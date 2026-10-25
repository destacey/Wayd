import React from 'react'
import { render, screen } from '@testing-library/react'
import MyIntakeRequestsPage from './page'

jest.mock('@/src/components/common', () => ({
  PageTitle: ({ title }: { title: string }) => <h1>{title}</h1>,
}))

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ hasPermissionClaim: () => true }),
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

let mockEmployeeId: string | null = 'e-1'
jest.mock('@/src/hooks', () => ({
  useDocumentTitle: jest.fn(),
  useLinkedEmployee: () => ({
    employeeId: mockEmployeeId,
    hasLinkedEmployee: mockEmployeeId !== null,
  }),
}))

jest.mock('@/src/components/hoc', () => ({
  authorizePage: (Component: React.ComponentType<any>) => Component,
  requireFeatureFlag: (Component: React.ComponentType<any>) => Component,
}))

// The grid has its own suite; here only what the page asks it to show matters.
jest.mock('../_components/intake-requests-grid', () => ({
  __esModule: true,
  default: ({ requests }: { requests?: { title: string }[] }) => (
    <ul>
      {requests?.map((r) => (
        <li key={r.title}>{r.title}</li>
      ))}
    </ul>
  ),
}))

const mockUseGetIntakeRequestsQuery = jest.fn()
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useGetIntakeRequestsQuery: (...args: unknown[]) =>
      mockUseGetIntakeRequestsQuery(...args),
    useGetNeedByBasesQuery: () => ({ data: [] }),
  }),
)

describe('MyIntakeRequestsPage', () => {
  beforeEach(() => {
    mockEmployeeId = 'e-1'
    mockUseGetIntakeRequestsQuery.mockReset()
    mockUseGetIntakeRequestsQuery.mockReturnValue({
      data: [{ title: 'New laptop' }],
      isLoading: false,
      refetch: jest.fn(),
    })
  })

  it("asks the server for the caller's own requests", () => {
    // Arrange / Act
    render(<MyIntakeRequestsPage />)

    // Assert
    expect(mockUseGetIntakeRequestsQuery).toHaveBeenCalledWith(
      { mine: true },
      { skip: false },
    )
    expect(screen.getByText('New laptop')).toBeInTheDocument()
  })

  it('explains why there is nothing to show for an account with no linked employee', () => {
    // Arrange
    mockEmployeeId = null

    // Act
    render(<MyIntakeRequestsPage />)

    // Assert
    expect(mockUseGetIntakeRequestsQuery).toHaveBeenCalledWith(
      { mine: true },
      { skip: true },
    )
    expect(
      screen.getByText('Your account is not linked to an employee record.'),
    ).toBeInTheDocument()
  })
})
