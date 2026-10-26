import React, { Suspense } from 'react'
import { act, render, screen } from '@testing-library/react'
import IntakeDeskDetailsPage from './page'

let mockSearchParams = new URLSearchParams()

jest.mock('next/navigation', () => ({
  notFound: jest.fn(),
  usePathname: () => '/demand-management/desks/1',
  useRouter: () => ({ replace: jest.fn(), push: jest.fn() }),
  useSearchParams: () => mockSearchParams,
}))

// jsdom has no matchMedia; pin a wide viewport so the rail renders.
jest.mock('antd', () => {
  const actual = jest.requireActual('antd')
  return {
    ...actual,
    Grid: {
      ...actual.Grid,
      useBreakpoint: () => ({ md: true, lg: true, xl: true }),
    },
  }
})

jest.mock('@/src/components/common', () => ({
  PageActions: ({ actionItems }: { actionItems: any[] }) => (
    <div data-testid="page-actions">
      {actionItems.map((item) => (
        <button key={item.key} type="button" onClick={item.onClick}>
          {item.label}
        </button>
      ))}
    </div>
  ),
}))

jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({
    hasClaim: jest.fn(() => true),
    hasPermissionClaim: jest.fn(() => true),
  }),
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: jest.fn(), success: jest.fn() }),
}))

jest.mock('@/src/hooks', () => ({
  ...jest.requireActual('@/src/hooks'),
  useDocumentTitle: jest.fn(),
}))

jest.mock('@/src/components/hoc', () => ({
  requireFeatureFlag: (Component: React.ComponentType<any>) => Component,
}))

// Grids have suites of their own; here only which actions the page offers matters.
jest.mock('../../_components/intake-requests-grid', () => ({
  __esModule: true,
  default: () => <div data-testid="requests-grid" />,
}))
jest.mock('@/src/components/common/wayd-grid', () => ({
  WaydGrid: () => <div data-testid="members-grid" />,
}))

let mockDesk: any
jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useGetIntakeDeskQuery: () => ({
      data: mockDesk,
      error: undefined,
      refetch: jest.fn(),
    }),
    useGetIntakeDeskVisibilitiesQuery: () => ({ data: [] }),
    useGetIntakeRequestsQuery: () => ({
      data: [],
      isLoading: false,
      refetch: jest.fn(),
    }),
    useGetNeedByBasesQuery: () => ({ data: [] }),
    useGetIntakeEmployeeOptionsQuery: () => ({ data: [] }),
    useUpdateIntakeDeskMutation: () => [jest.fn()],
    useDeleteIntakeDeskMutation: () => [jest.fn()],
  }),
)

const desk = (overrides: Record<string, unknown> = {}) => ({
  id: 'd-1',
  key: 1,
  name: 'IT',
  description: 'Laptops, access and software',
  visibility: 'Open',
  isOpen: true,
  isRequesterSelectable: true,
  requesterMayAddParticipants: true,
  members: [{ employee: { id: 'e-1', key: 1, name: 'Avery' }, role: 'Admin' }],
  canManage: false,
  canAdminister: false,
  canDelete: false,
  ...overrides,
})

const renderPage = async () =>
  await act(async () =>
    render(
      <Suspense fallback={<div>Loading desk</div>}>
        <IntakeDeskDetailsPage params={Promise.resolve({ key: '1' })} />
      </Suspense>,
    ),
  )

describe('IntakeDeskDetailsPage', () => {
  beforeEach(() => {
    mockSearchParams = new URLSearchParams()
    mockDesk = desk()
  })

  it('offers no change to someone who does not run the desk', async () => {
    // Arrange
    mockSearchParams = new URLSearchParams('section=members')

    // Act
    await renderPage()

    // Assert
    expect(await screen.findByTestId('members-grid')).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Edit Members' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Delete' }),
    ).not.toBeInTheDocument()
  })

  it.each([
    ['members', 'Edit Members'],
    ['settings', 'Edit Settings'],
  ])(
    'offers a Desk Admin the %s section action',
    async (section, actionLabel) => {
      // Arrange
      mockDesk = desk({ canManage: true })
      mockSearchParams = new URLSearchParams(`section=${section}`)

      // Act
      await renderPage()

      // Assert
      expect(
        await screen.findByRole('button', { name: actionLabel }),
      ).toBeInTheDocument()
    },
  )

  it('offers Delete only where the server allows it', async () => {
    // Arrange
    mockDesk = desk({ canDelete: true })

    // Act
    await renderPage()

    // Assert
    expect(
      await screen.findByRole('button', { name: 'Delete' }),
    ).toBeInTheDocument()
  })

  it('warns when nobody works the desk', async () => {
    // Arrange
    mockDesk = desk({ members: [], waiting: { count: 1 } })

    // Act
    await renderPage()

    // Assert
    expect(
      await screen.findByText('Nobody works this desk'),
    ).toBeInTheDocument()
  })
})
