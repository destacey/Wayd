import type { Mock } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  PokerRoundDto,
  PokerRoundPublicationOutcome,
  PokerRoundSubjectKind,
  PokerSessionDetailsDto,
  PokerSessionHostKind,
} from '@/src/services/wayd-api'
import { usePublishPokerRoundsMutation } from '@/src/store/features/planning/poker-sessions-api'
import SessionSidebar from './session-sidebar'

vi.mock('@/src/store/features/planning/poker-sessions-api', () => ({
  usePublishPokerRoundsMutation: vi.fn(),
}))

const mockMessage = { success: vi.fn(), warning: vi.fn(), error: vi.fn() }
vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => mockMessage,
}))

// Each has a suite of its own, or reads the store; here only the sidebar's own actions matter.
vi.mock('./session-timeline', () => ({ default: () => null }))
vi.mock('./session-summary', () => ({ default: () => null }))
let mockFormProps: any
vi.mock(
  '@/src/app/demand-management/_components/desk-poker-session-form',
  () => ({
    default: (props: any) => {
      mockFormProps = props
      return <div data-testid="add-requests-form" />
    },
  }),
)

const round = (
  id: string,
  requestId: string | undefined,
  overrides: Partial<PokerRoundDto> = {},
): PokerRoundDto => ({
  id,
  subject: requestId
    ? {
        kind: PokerRoundSubjectKind.IntakeRequest,
        id: requestId,
        displayKey: requestId,
      }
    : undefined,
  status: 'Accepted',
  consensusEstimate: 'M',
  order: 0,
  voteCount: 0,
  votes: [],
  ...overrides,
})

const publication = (outcome: PokerRoundPublicationOutcome) => ({
  outcome,
  at: new Date('2026-10-01T08:00:00Z'),
  byEmployeeId: 'e-1',
})

const session = (
  rounds: PokerRoundDto[],
  hosted = true,
): PokerSessionDetailsDto =>
  ({
    id: 'session-1',
    key: 7,
    host: hosted
      ? { kind: PokerSessionHostKind.IntakeDesk, id: 'd-1' }
      : undefined,
    rounds,
  }) as unknown as PokerSessionDetailsDto

const renderSidebar = (
  shown: PokerSessionDetailsDto,
  overrides: { canManage?: boolean; isActive?: boolean } = {},
) =>
  render(
    <SessionSidebar
      session={shown}
      onSelectRound={() => {}}
      onRemoveRound={() => {}}
      onComplete={() => {}}
      isCompleting={false}
      canManage={overrides.canManage ?? true}
      isActive={overrides.isActive ?? true}
    />,
  )

describe('SessionSidebar', () => {
  const mockPublish = vi.fn()

  beforeEach(() => {
    mockPublish.mockReset()
    mockFormProps = undefined
    Object.values(mockMessage).forEach((m) => m.mockReset())
    ;(usePublishPokerRoundsMutation as Mock).mockReturnValue([
      mockPublish,
      { isLoading: false },
    ])
  })

  it('counts only accepted rounds about a record that are not yet published', () => {
    // Arrange / Act
    renderSidebar(
      session([
        round('r1', '41'),
        round('r2', '42', {
          publication: publication(PokerRoundPublicationOutcome.Published),
        }),
        round('r3', '43', { status: 'Voting' }),
        round('r4', undefined),
        round('r5', '45', {
          publication: publication(PokerRoundPublicationOutcome.Refused),
        }),
      ]),
    )

    // Assert
    expect(
      screen.getByRole('button', { name: 'Publish 2 estimates' }),
    ).toBeInTheDocument()
  })

  it('publishes every round of the session and says how many were refused', async () => {
    // Arrange
    mockPublish.mockReturnValue({
      unwrap: () =>
        Promise.resolve([
          round('r1', '41', {
            publication: publication(PokerRoundPublicationOutcome.Published),
          }),
          round('r2', '42', {
            publication: publication(PokerRoundPublicationOutcome.Refused),
          }),
        ]),
    })
    renderSidebar(session([round('r1', '41'), round('r2', '42')]))

    // Act
    await userEvent.click(
      screen.getByRole('button', { name: 'Publish 2 estimates' }),
    )

    // Assert
    expect(mockPublish).toHaveBeenCalledWith({
      sessionId: 'session-1',
      sessionKey: 7,
    })
    expect(mockMessage.warning).toHaveBeenCalledWith(
      '1 published, 1 refused. Open a refused round to see why.',
    )
  })

  it('still offers publishing once the session is completed, but no more requests', () => {
    // Arrange / Act
    renderSidebar(session([round('r1', '41')]), { isActive: false })

    // Assert
    expect(
      screen.getByRole('button', { name: 'Publish 1 estimate' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Add requests' }),
    ).not.toBeInTheDocument()
  })

  it('adds requests to the desk’s session, leaving out those it already has', async () => {
    // Arrange
    renderSidebar(session([round('r1', '41'), round('r2', undefined)]))

    // Act
    await userEvent.click(screen.getByRole('button', { name: 'Add requests' }))

    // Assert
    expect(screen.getByTestId('add-requests-form')).toBeInTheDocument()
    expect(mockFormProps.desk.id).toBe('d-1')
    expect(mockFormProps.session).toEqual({ id: 'session-1' })
    expect(mockFormProps.excludedRequestIds).toEqual(['41'])
  })

  it('offers no desk requests in a session no desk hosts', () => {
    // Arrange / Act
    renderSidebar(session([round('r1', undefined)], false))

    // Assert
    expect(
      screen.queryByRole('button', { name: 'Add requests' }),
    ).not.toBeInTheDocument()
  })

  it('offers neither action to someone who does not run the session', () => {
    // Arrange / Act
    renderSidebar(session([round('r1', '41')]), { canManage: false })

    // Assert
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })
})
