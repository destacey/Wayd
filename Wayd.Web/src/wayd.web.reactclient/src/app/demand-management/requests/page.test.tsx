import React from 'react'
import { render, screen } from '@testing-library/react'
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

// The grid and the desk form have suites of their own; here only which actions the page offers matters.
jest.mock('../_components/intake-requests-grid', () => ({
  __esModule: true,
  default: () => <div data-testid="requests-grid" />,
}))
jest.mock('../_components/create-intake-desk-form', () => ({
  __esModule: true,
  default: () => <div data-testid="create-desk-form" />,
}))

jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useGetIntakeRequestsQuery: () => ({
      data: [],
      isLoading: false,
      refetch: jest.fn(),
    }),
    useGetNeedByBasesQuery: () => ({ data: [] }),
  }),
)

describe('IntakeRequestsPage', () => {
  beforeEach(() => {
    mockClaims = new Set(['Permissions.IntakeRequests.View'])
    mockEmployeeId = 'e-1'
  })

  it('offers neither action to someone who can only view requests', () => {
    // Arrange / Act
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

  it('offers Submit a Request to a linked employee who can create requests', () => {
    // Arrange
    mockClaims.add('Permissions.IntakeRequests.Create')

    // Act
    render(<IntakeRequestsPage />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Submit a Request' }),
    ).toBeInTheDocument()
  })

  it('withholds Submit a Request from an account with no linked employee', () => {
    // Arrange
    mockClaims.add('Permissions.IntakeRequests.Create')
    mockEmployeeId = null

    // Act
    render(<IntakeRequestsPage />)

    // Assert
    expect(
      screen.queryByRole('button', { name: 'Submit a Request' }),
    ).not.toBeInTheDocument()
  })

  it('offers Create Desk only to an administrator', () => {
    // Arrange
    mockClaims.add('Permissions.IntakeRequests.Administer')

    // Act
    render(<IntakeRequestsPage />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Create Desk' }),
    ).toBeInTheDocument()
  })
})
