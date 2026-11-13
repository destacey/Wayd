import type { Mock } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  PokerRoundDto,
  PokerRoundPublicationOutcome,
  PokerRoundSubjectKind,
} from '@/src/services/wayd-api'
import { usePublishPokerRoundsMutation } from '@/src/store/features/planning/poker-sessions-api'
import RoundPublication, { canPublishRound } from './round-publication'

vi.mock('@/src/store/features/planning/poker-sessions-api', () => ({
  usePublishPokerRoundsMutation: vi.fn(),
}))

const mockMessage = { success: vi.fn(), warning: vi.fn(), error: vi.fn() }
vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => mockMessage,
}))

const subject = {
  kind: PokerRoundSubjectKind.IntakeRequest,
  id: 'r-42',
  displayKey: '42',
  title: 'Export invoices',
}

const round = (overrides: Partial<PokerRoundDto> = {}): PokerRoundDto => ({
  id: 'round-1',
  subject,
  status: 'Accepted',
  consensusEstimate: 'M',
  order: 0,
  voteCount: 0,
  votes: [],
  ...overrides,
})

const publication = (
  outcome: PokerRoundPublicationOutcome,
  refusalReason?: string,
) => ({
  outcome,
  at: new Date('2026-10-01T08:00:00Z'),
  byEmployeeId: 'e-1',
  by: { id: 'e-1', key: 1, name: 'Avery Agent' },
  refusalReason,
})

const props = {
  sessionId: 'session-1',
  sessionKey: 7,
  canManage: true,
  consensusIsCard: false,
}

describe('RoundPublication', () => {
  const mockPublish = vi.fn()

  beforeEach(() => {
    mockPublish.mockReset()
    Object.values(mockMessage).forEach((m) => m.mockReset())
    ;(usePublishPokerRoundsMutation as Mock).mockReturnValue([
      mockPublish,
      { isLoading: false },
    ])
  })

  it('publishes the round to its request and says what the request now holds', async () => {
    // Arrange
    mockPublish.mockReturnValue({
      unwrap: () =>
        Promise.resolve([
          round({
            publication: publication(PokerRoundPublicationOutcome.Published),
          }),
        ]),
    })
    render(<RoundPublication {...props} round={round()} />)

    // Act
    await userEvent.click(
      screen.getByRole('button', { name: 'Publish to request 42' }),
    )

    // Assert
    expect(mockPublish).toHaveBeenCalledWith({
      sessionId: 'session-1',
      sessionKey: 7,
      roundId: 'round-1',
    })
    expect(mockMessage.success).toHaveBeenCalledWith(
      'Request 42 is estimated at M.',
    )
  })

  it('warns with the reason when the request’s desk refuses the estimate', async () => {
    // Arrange
    mockPublish.mockReturnValue({
      unwrap: () =>
        Promise.resolve([
          round({
            publication: publication(
              PokerRoundPublicationOutcome.Refused,
              'The request has moved to another desk.',
            ),
          }),
        ]),
    })
    render(<RoundPublication {...props} round={round()} />)

    // Act
    await userEvent.click(
      screen.getByRole('button', { name: 'Publish to request 42' }),
    )

    // Assert
    expect(mockMessage.warning).toHaveBeenCalledWith(
      'The request has moved to another desk.',
    )
    expect(mockMessage.success).not.toHaveBeenCalled()
  })

  it('says a card clears the request’s estimate before it is published', () => {
    // Arrange / Act
    render(
      <RoundPublication
        {...props}
        consensusIsCard
        round={round({ consensusEstimate: '?' })}
      />,
    )

    // Assert
    expect(
      screen.getByText(
        "Not published. Publishing ? clears request 42's estimate.",
      ),
    ).toBeInTheDocument()
  })

  it('shows who published a published round, with nothing left to publish', () => {
    // Arrange / Act
    render(
      <RoundPublication
        {...props}
        round={round({
          publication: publication(PokerRoundPublicationOutcome.Published),
        })}
      />,
    )

    // Assert
    expect(screen.getByText('Published')).toBeInTheDocument()
    expect(screen.getByText(/by Avery Agent on/)).toBeInTheDocument()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('shows why a refused round was not published and offers to publish it again', () => {
    // Arrange / Act
    render(
      <RoundPublication
        {...props}
        round={round({
          publication: publication(
            PokerRoundPublicationOutcome.Refused,
            'A closed request cannot be estimated.',
          ),
        })}
      />,
    )

    // Assert
    expect(screen.getByText('Not published')).toBeInTheDocument()
    expect(
      screen.getByText('A closed request cannot be estimated.'),
    ).toBeInTheDocument()
    expect(
      screen.getByRole('button', { name: 'Publish again' }),
    ).toBeInTheDocument()
  })

  it('offers no publish action to someone who does not run the session', () => {
    // Arrange / Act
    render(<RoundPublication {...props} canManage={false} round={round()} />)

    // Assert
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
    expect(screen.getByText(/^Not published\./)).toBeInTheDocument()
  })

  it.each([
    ['a free-text round', round({ subject: undefined })],
    ['a round not yet accepted', round({ status: 'Revealed' })],
  ])('renders nothing for %s', (_what, shown) => {
    // Arrange / Act
    const { container } = render(<RoundPublication {...props} round={shown} />)

    // Assert
    expect(container).toBeEmptyDOMElement()
  })
})

describe('canPublishRound', () => {
  it.each([
    ['an accepted round about a record', round(), true],
    [
      'a refused round',
      round({
        publication: publication(PokerRoundPublicationOutcome.Refused, 'No.'),
      }),
      true,
    ],
    [
      'a published round',
      round({
        publication: publication(PokerRoundPublicationOutcome.Published),
      }),
      false,
    ],
    ['a free-text round', round({ subject: undefined }), false],
    ['a round still voting', round({ status: 'Voting' }), false],
  ])('is as expected for %s', (_what, candidate, expected) => {
    // Act
    const result = canPublishRound(candidate)

    // Assert
    expect(result).toBe(expected)
  })
})
