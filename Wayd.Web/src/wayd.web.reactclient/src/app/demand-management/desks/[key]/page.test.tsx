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

let mockPokerEnabled = false
vi.mock(import('@/src/hooks'), async (importOriginal) => ({
  ...(await importOriginal()),
  useDocumentTitle: vi.fn(),
  useFeatureFlag: () => ({ isEnabled: mockPokerEnabled, isLoading: false }),
  useLinkedEmployee: () => ({ employeeId: 'e-1', hasLinkedEmployee: true }),
}))

// Lists sessions from the poker API, which this suite does not provide.
vi.mock('./_components/intake-desk-poker-sessions', () => ({
  default: () => <div data-testid="poker-sessions" />,
}))
vi.mock('../../_components/desk-poker-session-form', () => ({
  default: () => <div data-testid="poker-session-form" />,
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
  channelIds: [],
  tagCategoryIds: [],
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
    mockPokerEnabled = false
  })

  describe('poker sessions', () => {
    const estimating = { estimationScale: { id: 's-1', name: 'T-Shirt' } }

    it('lists the desk’s sessions and lets a member start one', async () => {
      // Arrange
      mockPokerEnabled = true
      mockDesk = desk(estimating)
      mockSearchParams = new URLSearchParams('section=poker-sessions')

      // Act
      await renderPage()

      // Assert
      expect(await screen.findByTestId('poker-sessions')).toBeInTheDocument()
      expect(
        screen.getByRole('button', { name: 'Start Poker Session' }),
      ).toBeInTheDocument()
    })

    it('withholds starting one from someone who does not work the desk', async () => {
      // Arrange
      mockPokerEnabled = true
      mockDesk = desk({
        ...estimating,
        members: [{ employee: { id: 'e-9', key: 9, name: 'Blair' }, role: 'Agent' }],
      })
      mockSearchParams = new URLSearchParams('section=poker-sessions')

      // Act
      await renderPage()

      // Assert
      expect(await screen.findByTestId('poker-sessions')).toBeInTheDocument()
      expect(
        screen.queryByRole('button', { name: 'Start Poker Session' }),
      ).not.toBeInTheDocument()
    })

    it('withholds starting one on a desk with no estimation scale', async () => {
      // Arrange
      mockPokerEnabled = true
      mockSearchParams = new URLSearchParams('section=poker-sessions')

      // Act
      await renderPage()

      // Assert
      expect(await screen.findByTestId('poker-sessions')).toBeInTheDocument()
      expect(
        screen.queryByRole('button', { name: 'Start Poker Session' }),
      ).not.toBeInTheDocument()
    })

    it('has no poker sessions section while Planning Poker is off', async () => {
      // Arrange
      mockDesk = desk(estimating)

      // Act
      await renderPage()

      // Assert
      expect(await screen.findByTestId('requests-grid')).toBeInTheDocument()
      expect(screen.queryByText('Poker Sessions')).not.toBeInTheDocument()
      expect(
        screen.queryByRole('button', { name: 'Start Poker Session' }),
      ).not.toBeInTheDocument()
    })
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
