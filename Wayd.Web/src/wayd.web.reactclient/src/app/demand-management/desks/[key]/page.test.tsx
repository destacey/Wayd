import React, { Suspense } from 'react'
import { act, render, screen } from '@testing-library/react'
import IntakeDeskDetailsPage from './page'

let mockSearchParams = new URLSearchParams()

vi.mock('next/navigation', () => ({
  notFound: vi.fn(),
  usePathname: () => '/demand-management/desks/1',
  useRouter: () => ({ replace: vi.fn(), push: vi.fn() }),
  useSearchParams: () => mockSearchParams,
}))

// jsdom has no matchMedia; pin a wide viewport so the rail renders.
vi.mock(import('antd'), async (importOriginal) => {
  const actual = await importOriginal()
  return {
    ...actual,
    Grid: {
      ...actual.Grid,
      useBreakpoint: () => ({ md: true, lg: true, xl: true }),
    },
  }
})

vi.mock('@/src/components/common', () => ({
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

vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({
    hasClaim: vi.fn(() => true),
    hasPermissionClaim: vi.fn(() => true),
  }),
}))

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

vi.mock(import('@/src/hooks'), async (importOriginal) => ({
  ...(await importOriginal()),
  useDocumentTitle: vi.fn(),
}))

vi.mock('@/src/components/hoc', () => ({
  requireFeatureFlag: (Component: React.ComponentType<any>) => Component,
}))

// Grids have suites of their own; here only which actions the page offers matters.
vi.mock('../../_components/intake-requests-grid', () => ({
  default: () => <div data-testid="requests-grid" />,
}))
vi.mock('@/src/components/common/wayd-grid', () => ({
  WaydGrid: () => <div data-testid="members-grid" />,
}))
// Reads its settings from the store, which this suite does not provide.
vi.mock('@/src/components/common/discussions', () => ({
  DiscussionSettingsPanel: () => <div data-testid="discussion-settings" />,
}))

let mockDesk: any
vi.mock('@/src/store/features/demand-management/demand-management-api', () => ({
  useGetIntakeDeskQuery: () => ({
    data: mockDesk,
    error: undefined,
    refetch: vi.fn(),
  }),
  useGetIntakeDeskVisibilitiesQuery: () => ({ data: [] }),
  useGetIntakeRequestsQuery: () => ({
    data: [],
    isLoading: false,
    refetch: vi.fn(),
  }),
  useGetNeedByBasesQuery: () => ({ data: [] }),
  useGetIntakeChannelsQuery: () => ({ data: [] }),
  useGetIntakeRequestTagCategoriesQuery: () => ({ data: [] }),
  useGetIntakeDeskWorkflowQuery: () => ({ data: undefined, isLoading: false }),
  useGetIntakeEmployeeOptionsQuery: () => ({ data: [] }),
  useUpdateIntakeDeskMutation: () => [vi.fn()],
  useDeleteIntakeDeskMutation: () => [vi.fn()],
}))

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
