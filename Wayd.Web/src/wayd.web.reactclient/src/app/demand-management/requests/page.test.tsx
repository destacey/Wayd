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
  WaydTooltip: ({ children }: { children: React.ReactNode }) => <>{children}</>,
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

// The grid has a suite of its own; here only what the page hands it matters: the rows, in order, and the
// sort it opens on.
jest.mock('../_components/intake-requests-grid', () => ({
  __esModule: true,
  default: ({
    rightSlot,
    requests,
    initialSorting,
  }: {
    rightSlot?: React.ReactNode
    requests?: { id: string; title: string }[]
    initialSorting?: unknown
  }) => (
    <div
      data-testid="requests-grid"
      data-sorting={JSON.stringify(initialSorting ?? null)}
    >
      {(requests ?? []).map((r) => (
        <span key={r.id} data-testid="row">
          {r.title}
        </span>
      ))}
      {rightSlot}
    </div>
  ),
}))

let mockSearch = new URLSearchParams()
const mockReplace = jest.fn()
jest.mock('next/navigation', () => ({
  useRouter: () => ({ replace: mockReplace }),
  usePathname: () => '/demand-management/requests',
  useSearchParams: () => mockSearch,
}))

let mockDesks: any[] = []
let mockScores: any[] = []
const mockRequestsQuery = jest.fn((_args?: unknown) => ({
  currentData: [],
  isFetching: false,
  refetch: jest.fn(),
}))
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useGetIntakeRequestsQuery: (args: unknown) => mockRequestsQuery(args),
    useGetIntakeRequestScoresQuery: () => ({ currentData: mockScores }),
    useGetNeedByBasesQuery: () => ({ data: [] }),
    useGetIntakeDesksQuery: () => ({ data: mockDesks }),
    useGetIntakeChannelsQuery: () => ({ data: [] }),
    useGetIntakeRequestTagCategoriesQuery: () => ({ data: [] }),
    useSearchIntakeRequestTagsQuery: () => ({ data: undefined }),
  }),
)

describe('IntakeRequestsPage', () => {
  beforeEach(() => {
    mockClaims = new Set()
    mockEmployeeId = 'e-1'
    mockRequestsQuery.mockClear()
    mockReplace.mockClear()
    mockSearch = new URLSearchParams()
    mockDesks = []
    mockScores = []
  })

  it('opens on the desks the reader works when the address names no filters', () => {
    // Arrange
    mockDesks = [
      {
        id: 'd-1',
        key: 1,
        name: 'IT',
        members: [
          { employee: { id: 'e-1', key: 1, name: 'Me' }, role: 'Agent' },
        ],
      },
    ]

    // Act
    render(<IntakeRequestsPage />)

    // Assert
    expect(mockReplace).toHaveBeenCalledWith(
      '/demand-management/requests?desk=mine',
      { scroll: false },
    )
  })

  it('keeps the filters an address names, and opens everyone else on every open request', () => {
    // Arrange
    mockDesks = [{ id: 'd-1', key: 1, name: 'IT', members: [] }]

    // Act
    render(<IntakeRequestsPage />)

    // Assert
    expect(mockReplace).not.toHaveBeenCalled()
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

  it('puts the board layout in the address', async () => {
    // Arrange
    render(<IntakeRequestsPage />)

    // Act
    await act(() => userEvent.click(screen.getByTitle('Board')))

    // Assert
    expect(mockReplace).toHaveBeenCalledWith(
      '/demand-management/requests?view=board',
      { scroll: false },
    )
  })

  it('reads a shared Assigned to me link as anyone for a reader with no employee record', async () => {
    // Arrange
    mockEmployeeId = null
    mockSearch = new URLSearchParams('assignee=me')
    render(<IntakeRequestsPage />)

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: /Filters/ })),
    )

    // Assert
    expect(screen.getByText('Anyone assigned or not')).toBeInTheDocument()
  })

  it('resets every filter from the reset button', async () => {
    // Arrange
    mockSearch = new URLSearchParams('scope=all&assignee=unassigned')
    render(<IntakeRequestsPage />)
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: /Filters/ })),
    )

    // Act
    await act(() =>
      userEvent.click(screen.getByRole('button', { name: 'Reset filters' })),
    )

    // Assert
    expect(mockReplace).toHaveBeenLastCalledWith(
      '/demand-management/requests',
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

  describe('ranked by score', () => {
    const request = (id: string, key: number) => ({
      id,
      key,
      title: id,
      status: { id: 's-1', name: 'Submitted', category: 'Proposed' },
      desk: { id: 'd-1', key: 1, name: 'IT' },
      requester: { id: 'e-r', key: 1, name: 'Requester' },
      tags: [],
    })
    const score = (requestId: string, value: number) => ({
      requestId,
      value,
      isDeskModel: true,
      scoringModel: { id: 'm-1', key: 1, name: 'WSJF' },
    })

    beforeEach(() => {
      mockRequestsQuery.mockReturnValue({
        currentData: [
          request('unscored', 1),
          request('low', 2),
          request('high', 3),
        ] as any,
        isFetching: false,
        refetch: jest.fn(),
      })
      mockScores = [score('low', 1.6), score('high', 3.3)]
    })

    afterEach(() => {
      mockRequestsQuery.mockReturnValue({
        currentData: [],
        isFetching: false,
        refetch: jest.fn(),
      })
    })

    it('hands the grid the requests highest score first, with no sort of its own', () => {
      // Arrange
      mockSearch = new URLSearchParams('order=score')

      // Act
      render(<IntakeRequestsPage />)

      // Assert
      expect(screen.getAllByTestId('row').map((r) => r.textContent)).toEqual([
        'high',
        'low',
        'unscored',
      ])
      expect(screen.getByTestId('requests-grid')).toHaveAttribute(
        'data-sorting',
        '[]',
      )
    })

    it('leaves the usual order to the grid', () => {
      // Act
      render(<IntakeRequestsPage />)

      // Assert
      expect(screen.getAllByTestId('row').map((r) => r.textContent)).toEqual([
        'unscored',
        'low',
        'high',
      ])
      expect(screen.getByTestId('requests-grid')).toHaveAttribute(
        'data-sorting',
        'null',
      )
    })
  })
})
