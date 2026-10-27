import React from 'react'
import { act, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import IntakeRequestsPage from './page'

jest.mock('@/src/components/common', () => ({
  PageTitle: ({
    title,
    actions,
  }: {
    title: string
    actions?: React.ReactNode
  }) => (
    <header>
      <h1>{title}</h1>
      {actions}
    </header>
  ),
}))

let mockClaims = new Set<string>()
jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({
    hasPermissionClaim: (claim: string) => mockClaims.has(claim),
  }),
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

// The grid has a suite of its own; here only which actions the page offers matters.
jest.mock('../_components/intake-requests-grid', () => ({
  __esModule: true,
  default: () => <div data-testid="requests-grid" />,
}))

let mockSearch = new URLSearchParams()
const mockReplace = jest.fn()
jest.mock('next/navigation', () => ({
  useRouter: () => ({ replace: mockReplace }),
  usePathname: () => '/demand-management/requests',
  useSearchParams: () => mockSearch,
}))

const mockRequestsQuery = jest.fn((_args?: unknown) => ({
  currentData: [],
  isFetching: false,
  refetch: jest.fn(),
}))
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useGetIntakeRequestsQuery: (args: unknown) => mockRequestsQuery(args),
    useGetNeedByBasesQuery: () => ({ data: [] }),
    useGetIntakeDesksQuery: () => ({ data: [] }),
    useGetIntakeChannelsQuery: () => ({ data: [] }),
  }),
)

describe('IntakeRequestsPage', () => {
  beforeEach(() => {
    mockClaims = new Set()
    mockEmployeeId = 'e-1'
    mockRequestsQuery.mockClear()
    mockReplace.mockClear()
    mockSearch = new URLSearchParams()
  })

  it('opens on the requests still open', () => {
    // Act
    render(<IntakeRequestsPage />)

    // Assert
    expect(mockRequestsQuery).toHaveBeenLastCalledWith({ openOnly: true })
  })

  it('puts a change of filter in the address', async () => {
    // Arrange
    render(<IntakeRequestsPage />)

    // Act — jsdom matches no breakpoint, so the filters are behind the phone layout's button
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: /Filters/ })),
    )
    await act(() => userEvent.click(screen.getByText('Everything')))

    // Assert
    expect(mockReplace).toHaveBeenCalledWith(
      '/demand-management/requests?scope=all',
      { scroll: false },
    )
  })

  it('includes closed requests when the address asks for everything', () => {
    // Arrange
    mockSearch = new URLSearchParams('scope=all')

    // Act
    render(<IntakeRequestsPage />)

    // Assert
    expect(mockRequestsQuery).toHaveBeenLastCalledWith({ openOnly: false })
  })

  it('offers no Create Desk, which lives on the Desks page, even to the Demand Management Admin', () => {
    // Arrange
    mockClaims.add('Permissions.IntakeRequests.Administer')
    mockEmployeeId = null

    // Act
    render(<IntakeRequestsPage />)

    // Assert
    expect(screen.getByTestId('requests-grid')).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Submit a Request' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Create Desk' }),
    ).not.toBeInTheDocument()
  })

  it('offers Submit a Request to a linked employee holding no permission', () => {
    // Arrange — submitting needs only an employee to be the submitter

    // Act
    render(<IntakeRequestsPage />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Submit a Request' }),
    ).toBeInTheDocument()
  })

  it('withholds Submit a Request from an account with no linked employee', () => {
    // Arrange
    mockEmployeeId = null

    // Act
    render(<IntakeRequestsPage />)

    // Assert
    expect(
      screen.queryByRole('button', { name: 'Submit a Request' }),
    ).not.toBeInTheDocument()
  })
})
