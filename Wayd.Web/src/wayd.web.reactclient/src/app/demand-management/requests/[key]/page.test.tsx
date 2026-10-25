import React, { Suspense } from 'react'
import { act, render, screen } from '@testing-library/react'
import { notFound } from 'next/navigation'
import IntakeRequestDetailsPage from './page'

// jest.setup stubs dayjs down to formatting; the participant dates and pickers need the real module.
jest.unmock('dayjs')

let mockSearchParams = new URLSearchParams()

jest.mock('next/navigation', () => ({
  notFound: jest.fn(),
  usePathname: () => '/demand-management/requests/42',
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
  canAddParticipants: false,
  ...overrides,
})

describe('IntakeRequestDetailsPage', () => {
  beforeEach(() => {
    mockSearchParams = new URLSearchParams()
    mockRequest = details()
    mockError = undefined
    jest.mocked(notFound).mockClear()
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

  it('treats a request the reader cannot see as not found', async () => {
    // Arrange
    mockRequest = undefined
    mockError = { status: 404, data: {} }

    // Act
    await renderPage()

    // Assert
    expect(notFound).toHaveBeenCalled()
  })

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
