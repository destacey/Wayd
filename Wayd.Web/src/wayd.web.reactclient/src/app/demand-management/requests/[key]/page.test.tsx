import React, { Suspense } from 'react'
import { act, render, screen, within } from '@testing-library/react'
import { notFound } from 'next/navigation'
import IntakeRequestDetailsPage from './page'

// jest.setup stubs dayjs down to formatting; the participant dates and pickers need the real module.
jest.unmock('dayjs')

let mockSearchParams = new URLSearchParams()
const mockReplace = jest.fn()
const mockRouter = { replace: mockReplace, push: jest.fn() }

jest.mock('next/navigation', () => ({
  notFound: jest.fn(),
  usePathname: () => '/demand-management/requests/42',
  useRouter: () => mockRouter,
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

// The markdown editor pulls in ESM-only packages Jest cannot parse.
jest.mock('@/src/components/common/markdown', () => ({
  MarkdownRenderer: ({ markdown }: { markdown: string }) => (
    <div data-testid="markdown">{markdown}</div>
  ),
  MarkdownEditor: () => <textarea data-testid="markdown-editor" />,
}))

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

// The real auth context imports the hooks barrel back, which the barrel's mock below cannot survive.
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
  authorizePage: (Component: React.ComponentType<any>) => Component,
  requireFeatureFlag: (Component: React.ComponentType<any>) => Component,
}))

const summary = {
  id: 'r-42',
  key: 42,
  title: 'Rotate the VPN certificates',
  desk: { id: 'd-1', key: 1, name: 'IT' },
  requester: { id: 'e-1', key: 1, name: 'Avery Example' },
  channel: { id: 'c-1', name: 'Security' },
  status: { id: 's-1', name: 'Submitted', category: 'Proposed', alias: 1 },
}

const substance = {
  description: 'The certificates expire in March.',
  submittedBy: { id: 'e-1', key: 1, name: 'Avery Example' },
  participants: [
    {
      employee: { id: 'e-2', key: 2, name: 'Blake Sample' },
      addedOn: new Date('2026-10-01T09:00:00Z'),
    },
  ],
}

let mockRequest: any
let mockError: any

jest.mock(
  '@/src/store/features/demand-management/demand-management-api',
  () => ({
    useGetIntakeRequestQuery: () => ({
      data: mockRequest,
      error: mockError,
      refetch: jest.fn(),
    }),
    useGetNeedByBasesQuery: () => ({ data: [] }),
    useGetNeedByBasisFamiliesQuery: () => ({ data: [] }),
    useGetIntakeRequestActivitiesQuery: () => ({
      currentData: undefined,
      isLoading: false,
    }),
    useLazyGetIntakeRequestActivitiesQuery: () => [jest.fn()],
    useRemoveIntakeRequestParticipantMutation: () => [
      jest.fn(),
      { isLoading: false },
    ],
    useAddIntakeRequestParticipantMutation: () => [jest.fn()],
    useUpdateIntakeRequestMutation: () => [jest.fn()],
    useGetIntakeEmployeeOptionsQuery: () => ({ data: [] }),
    useGetIntakeChannelsQuery: () => ({ data: [] }),
    useGetIntakeRequestStatusOptionsQuery: () => ({
      data: [],
      isFetching: false,
    }),
    useChangeIntakeRequestStatusMutation: () => [
      jest.fn(),
      { isLoading: false },
    ],
  }),
)

// The page reads its params with use(), which suspends on first render.
const renderPage = async () =>
  await act(async () =>
    render(
      <Suspense fallback={<div>Loading request</div>}>
        <IntakeRequestDetailsPage params={Promise.resolve({ key: '42' })} />
      </Suspense>,
    ),
  )

const details = (overrides: Record<string, unknown> = {}) => ({
  summary,
  substance,
  canEdit: false,
  canRemoveParticipants: false,
  canDelete: false,
  canReroute: false,
  canAddParticipants: false,
  canChangeStatus: false,
  canChangeAssignee: false,
  canWithdraw: false,
  isDeskAgent: false,
  ...overrides,
})

describe('IntakeRequestDetailsPage', () => {
  beforeEach(() => {
    mockSearchParams = new URLSearchParams()
    mockRequest = details()
    mockError = undefined
    jest.mocked(notFound).mockClear()
    mockReplace.mockClear()
  })

  it('says the detail is limited to the desk when the substance is withheld', async () => {
    // Arrange
    mockRequest = details({ substance: null })

    // Act
    await renderPage()

    // Assert
    expect(
      await screen.findByText(
        'The detail of this request is limited to the IT desk.',
      ),
    ).toBeInTheDocument()
    expect(screen.queryByTestId('markdown')).not.toBeInTheDocument()
    expect(screen.queryByText('People')).not.toBeInTheDocument()
  })

  it('shows the description when the substance is readable', async () => {
    // Arrange / Act
    await renderPage()

    // Assert
    expect(
      await screen.findByText('The certificates expire in March.'),
    ).toBeInTheDocument()
    expect(screen.queryByText(/limited to the IT desk/)).not.toBeInTheDocument()
  })

  it('offers Edit only when the server says the reader may edit', async () => {
    // Arrange
    mockRequest = details({ canEdit: true })

    // Act
    await renderPage()

    // Assert
    expect(
      await screen.findByRole('button', { name: 'Edit' }),
    ).toBeInTheDocument()
  })

  it('offers no Edit when the server does not allow it', async () => {
    // Arrange / Act
    await renderPage()

    // Assert
    expect(
      await screen.findByText('Rotate the VPN certificates'),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Edit' }),
    ).not.toBeInTheDocument()
  })

  it('offers Move to and Withdraw only where the server allows each', async () => {
    // Arrange
    mockRequest = details({ canChangeStatus: true })

    // Act
    await renderPage()

    // Assert
    expect(
      await screen.findByRole('button', { name: /Move to/ }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Withdraw Request' }),
    ).not.toBeInTheDocument()
  })

  it('offers Change Assignee only when the server allows it', async () => {
    // Arrange
    mockRequest = details({ canChangeAssignee: true })

    // Act
    await renderPage()

    // Assert
    expect(
      await screen.findByRole('button', { name: 'Change Assignee' }),
    ).toBeInTheDocument()
  })

  it('offers no Change Assignee when the server does not allow it', async () => {
    // Arrange
    mockRequest = details({ canChangeStatus: true })

    // Act
    await renderPage()

    // Assert
    await screen.findByRole('button', { name: /Move to/ })
    expect(
      screen.queryByRole('button', { name: 'Change Assignee' }),
    ).not.toBeInTheDocument()
  })

  it('offers the requester Withdraw when the server allows it', async () => {
    // Arrange
    mockRequest = details({ canWithdraw: true })

    // Act
    await renderPage()

    // Assert
    expect(
      await screen.findByRole('button', { name: 'Withdraw Request' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: /Move to/ }),
    ).not.toBeInTheDocument()
  })

  it('leads back through the request’s desk, between Requests and the request', async () => {
    // Act
    await renderPage()

    // Assert
    const trail = (await screen.findAllByRole('navigation')).find((nav) =>
      within(nav).queryByRole('link', { name: 'Requests' }),
    )!
    expect(within(trail).getByRole('link', { name: 'IT' })).toHaveAttribute(
      'href',
      '/demand-management/desks/1',
    )
  })

  it('shows the status and the reason the requester reads', async () => {
    // Arrange
    mockRequest = details({
      summary: {
        ...summary,
        status: { id: 's-9', name: 'Declined', category: 'Removed', alias: 6 },
      },
      substance: { ...substance, statusReason: 'Budget went elsewhere' },
    })

    // Act
    await renderPage()

    // Assert
    expect((await screen.findAllByText('Declined')).length).toBeGreaterThan(0)
    expect(screen.getByText('Budget went elsewhere')).toBeInTheDocument()
  })

  it('takes a reader of a duplicate to the request it gives way to', async () => {
    // Arrange
    mockRequest = details({
      duplicateOf: { id: 'r-7', key: 7, title: 'Export invoices' },
    })

    // Act
    await renderPage()

    // Assert
    expect(mockReplace).toHaveBeenCalledWith(
      '/demand-management/requests/7?duplicate=42',
    )
    expect(
      screen.queryByText('Rotate the VPN certificates'),
    ).not.toBeInTheDocument()
  })

  it('keeps an agent on a duplicate, linking to the request it gives way to', async () => {
    // Arrange — the Demand Management Admin with no linked employee works the desk but cannot change status
    mockRequest = details({
      isDeskAgent: true,
      duplicateOf: { id: 'r-7', key: 7, title: 'Export invoices' },
    })

    // Act
    await renderPage()

    // Assert
    expect(
      await screen.findByRole('link', { name: '7 · Export invoices' }),
    ).toHaveAttribute('href', '/demand-management/requests/7')
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('keeps a reader on a duplicate whose surviving request they cannot see', async () => {
    // Arrange — the server names no surviving request to such a reader
    mockRequest = details({
      summary: {
        ...summary,
        status: { id: 's-7', name: 'Duplicate', category: 'Removed', alias: 7 },
      },
      duplicateOf: null,
    })

    // Act
    await renderPage()

    // Assert
    expect(
      await screen.findByText('Rotate the VPN certificates'),
    ).toBeInTheDocument()
    expect(mockReplace).not.toHaveBeenCalled()
  })

  it('says which duplicate brought the reader here', async () => {
    // Arrange
    mockSearchParams = new URLSearchParams('duplicate=12')

    // Act
    await renderPage()

    // Assert
    expect(
      await screen.findByText(
        'Request 12 was marked a duplicate of this one, so you were brought here.',
      ),
    ).toBeInTheDocument()
  })

  it('treats a request the reader cannot see as not found', async () => {
    // Arrange
    mockRequest = undefined
    mockError = { status: 404, data: {} }

    // Act
    await renderPage()

    // Assert
    expect(notFound).toHaveBeenCalled()
  })

  it.each([
    [true, true],
    [false, false],
  ])(
    'offers Move to Another Desk only where the server allows it (canReroute %s)',
    async (canReroute, expected) => {
      // Arrange
      mockRequest = details({ canReroute })

      // Act
      await renderPage()

      // Assert
      await screen.findByTestId('page-actions')
      expect(
        screen.queryByRole('button', { name: 'Move to Another Desk' }) !== null,
      ).toBe(expected)
    },
  )

  describe('people section', () => {
    beforeEach(() => {
      mockSearchParams = new URLSearchParams('section=participants')
    })

    it('offers Add Participant and Remove only where the server allows each', async () => {
      // Arrange
      mockRequest = details({
        canAddParticipants: true,
        canRemoveParticipants: false,
      })

      // Act
      await renderPage()

      // Assert
      expect(
        await screen.findByRole('button', { name: 'Add Participant' }),
      ).toBeInTheDocument()
      expect(
        screen.queryByRole('button', { name: 'Remove' }),
      ).not.toBeInTheDocument()
    })

    it('lets an agent remove a participant but never the requester', async () => {
      // Arrange
      mockRequest = details({ canRemoveParticipants: true })

      // Act
      await renderPage()

      // Assert
      expect(await screen.findByText('Blake Sample')).toBeInTheDocument()
      expect(screen.getAllByRole('button', { name: 'Remove' })).toHaveLength(1)
      expect(
        screen.queryByRole('button', { name: 'Add Participant' }),
      ).not.toBeInTheDocument()
    })
  })
})
