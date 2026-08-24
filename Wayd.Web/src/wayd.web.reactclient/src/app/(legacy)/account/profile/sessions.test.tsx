// Mocks must precede the import of the component under test.

// jest.setup stubs dayjs down to formatting, which leaves antd's Table without
// dayjs.isDayjs. The grid needs the real module.
jest.unmock('dayjs')

const mockLogout = jest.fn()
const mockRevokeSession = jest.fn()
const mockRevokeAllSessions = jest.fn()
const mockRefetch = jest.fn()

let mockQueryState: {
  data?: unknown[]
  isLoading: boolean
  error?: unknown
} = { data: [], isLoading: false }

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: jest.fn(), error: jest.fn() }),
}))

// The real auth context reaches the OIDC client registry and the browser's
// location, neither of which belongs in a unit test of this tab.
jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({ logout: mockLogout }),
}))

jest.mock('@/src/store/features/user-management/user-sessions-api', () => ({
  useGetMySessionsQuery: () => ({ ...mockQueryState, refetch: mockRefetch }),
  useRevokeSessionMutation: () => [
    mockRevokeSession.mockReturnValue({ unwrap: () => Promise.resolve() }),
  ],
  useRevokeAllSessionsMutation: () => [
    mockRevokeAllSessions.mockReturnValue({ unwrap: () => Promise.resolve() }),
  ],
}))

import { render, screen } from '@testing-library/react'
import Sessions from './sessions'

const session = (overrides: Record<string, unknown> = {}) => ({
  id: '11111111-1111-1111-1111-111111111111',
  deviceLabel: 'Chrome on Windows',
  ipAddress: '203.0.113.42',
  createdAt: '2026-03-01T12:00:00Z',
  lastUsedAt: '2026-03-01T12:30:00Z',
  expiresAt: '2026-03-08T12:00:00Z',
  isCurrent: false,
  ...overrides,
})

describe('Sessions', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockQueryState = { data: [], isLoading: false }
  })

  it('states that personal access tokens are managed separately', () => {
    // Sign-out does not revoke PATs, and someone reviewing their sessions is
    // often worried about compromise — the wrong moment to leave that unsaid.

    // Arrange
    mockQueryState = { data: [session()], isLoading: false }

    // Act
    render(<Sessions />)

    // Assert
    expect(
      screen.getByText(/personal access tokens are managed separately/i),
    ).toBeInTheDocument()
  })

  it('warns that signing out is not immediate', () => {
    // Arrange
    mockQueryState = { data: [session()], isLoading: false }

    // Act
    render(<Sessions />)

    // Assert
    expect(screen.getByText(/up to an hour/i)).toBeInTheDocument()
  })

  it('marks the current session', () => {
    // Arrange
    mockQueryState = {
      data: [
        session({ isCurrent: true }),
        session({ id: '22222222-2222-2222-2222-222222222222' }),
      ],
      isLoading: false,
    }

    // Act
    render(<Sessions />)

    // Assert
    expect(screen.getByText('This device')).toBeInTheDocument()
  })

  it('renders a row per session', () => {
    // Arrange
    mockQueryState = {
      data: [
        session({ deviceLabel: 'Chrome on Windows' }),
        session({
          id: '22222222-2222-2222-2222-222222222222',
          deviceLabel: 'Safari on iPhone',
        }),
      ],
      isLoading: false,
    }

    // Act
    render(<Sessions />)

    // Assert
    expect(screen.getByText('Chrome on Windows')).toBeInTheDocument()
    expect(screen.getByText('Safari on iPhone')).toBeInTheDocument()
  })

  it('falls back to a placeholder when a session has no device label', () => {
    // Background and CLI sign-ins have no user agent; the row must stay usable.

    // Arrange
    mockQueryState = {
      data: [session({ deviceLabel: null })],
      isLoading: false,
    }

    // Act
    render(<Sessions />)

    // Assert
    expect(screen.getByText('Unknown device')).toBeInTheDocument()
  })

  it('disables sign-out-everywhere when there are no sessions', () => {
    // Arrange
    mockQueryState = { data: [], isLoading: false }

    // Act
    render(<Sessions />)

    // Assert
    expect(
      screen.getByRole('button', { name: /sign out of all devices/i }),
    ).toBeDisabled()
  })

  it('shows an error state when the sessions query fails', () => {
    // Arrange
    mockQueryState = { data: undefined, isLoading: false, error: new Error('nope') }

    // Act
    render(<Sessions />)

    // Assert
    expect(screen.getByText(/unable to load sessions/i)).toBeInTheDocument()
  })
})
