import React from 'react'
import { render, screen } from '@testing-library/react'
import IntakeDesksPage from './page'

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

jest.mock('@/src/hooks', () => ({
  useDocumentTitle: jest.fn(),
}))

jest.mock('@/src/components/hoc', () => ({
  requireFeatureFlag: (Component: React.ComponentType<any>) => Component,
}))

// The grid has a suite of its own; render the key and name cells and the waiting text, found by id.
jest.mock('@/src/components/common/wayd-grid', () => ({
  WaydGrid: ({ columns, data }: { columns: any[]; data: any[] }) => {
    const column = (id: string) => columns.find((c) => c.id === id)
    return (
      <div data-testid="desks-grid">
        {(data ?? []).map((row) => (
          <div key={row.id}>
            {column('key').cell({ row: { original: row } })}
            {column('name').cell({ row: { original: row } })}
            <span>{column('waiting').accessorFn(row)}</span>
          </div>
        ))}
      </div>
    )
  },
}))

jest.mock('../_components/create-intake-desk-form', () => ({
  __esModule: true,
  default: () => <div data-testid="create-desk-form" />,
}))

let mockDesks: any[] = []
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useGetIntakeDesksQuery: () => ({
      data: mockDesks,
      isLoading: false,
      refetch: jest.fn(),
    }),
    useGetIntakeDeskVisibilitiesQuery: () => ({ data: [] }),
  }),
)

const desk = (overrides: Record<string, unknown> = {}) => ({
  id: 'd-1',
  key: 1,
  name: 'IT',
  description: 'Laptops',
  visibility: 'Open',
  isOpen: true,
  isRequesterSelectable: true,
  requesterMayAddParticipants: true,
  members: [{ employee: { id: 'e-1', key: 1, name: 'Avery' }, role: 'Agent' }],
  canManage: false,
  canAdminister: false,
  canDelete: false,
  ...overrides,
})

describe('IntakeDesksPage', () => {
  beforeEach(() => {
    mockClaims = new Set()
    mockDesks = []
  })

  it('offers Create Desk only to the Demand Management Admin', () => {
    // Arrange
    mockClaims.add('Permissions.IntakeRequests.Administer')

    // Act
    render(<IntakeDesksPage />)

    // Assert
    expect(
      screen.getByRole('button', { name: 'Create Desk' }),
    ).toBeInTheDocument()
  })

  it('withholds Create Desk from everyone else', () => {
    // Arrange
    mockClaims.add('Permissions.IntakeRequests.View')

    // Act
    render(<IntakeDesksPage />)

    // Assert
    expect(
      screen.queryByRole('button', { name: 'Create Desk' }),
    ).not.toBeInTheDocument()
  })

  it('links each desk key to its page', () => {
    // Arrange
    mockDesks = [desk({ id: 'd-7', key: 7, name: 'Security' })]

    // Act
    render(<IntakeDesksPage />)

    // Assert
    expect(screen.getByRole('link', { name: '7' })).toHaveAttribute(
      'href',
      '/demand-management/desks/7',
    )
  })

  it('flags a desk with no members and shows what waits on it where the server sent it', () => {
    // Arrange
    mockDesks = [
      desk({ id: 'd-1', name: 'Unowned', members: [], waiting: { count: 2 } }),
      desk({ id: 'd-2', name: 'Staffed' }),
    ]

    // Act
    render(<IntakeDesksPage />)

    // Assert
    expect(screen.getAllByText('No members')).toHaveLength(1)
    expect(screen.getByText('2 waiting')).toBeInTheDocument()
  })
})
