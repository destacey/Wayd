import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { PokerRoundDto } from '@/src/services/wayd-api'
import VotingActions from './voting-actions'
import {
  useRevealPokerRoundMutation,
  useResetPokerRoundMutation,
  useSetPokerRoundConsensusMutation,
} from '@/src/store/features/planning/poker-sessions-api'

jest.mock('@/src/store/features/planning/poker-sessions-api', () => ({
  useRevealPokerRoundMutation: jest.fn(),
  useResetPokerRoundMutation: jest.fn(),
  useSetPokerRoundConsensusMutation: jest.fn(),
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({
    success: jest.fn(),
    error: jest.fn(),
  }),
}))

function makeRound(
  status: string,
  overrides?: Partial<PokerRoundDto>,
): PokerRoundDto {
  return {
    id: 'round-1',
    label: 'Test',
    status,
    order: 1,
    voteCount: 0,
    votes: [],
    ...overrides,
  }
}

const defaultProps = {
  sessionId: 'session-1',
  sessionKey: 1,
  estimationScaleValues: ['1', '2', '3', '5', '8', '13'],
  canManage: true,
}

describe('VotingActions', () => {
  const mockReveal = jest.fn().mockResolvedValue({})
  const mockReset = jest.fn().mockResolvedValue({})
  const mockSetConsensus = jest.fn().mockResolvedValue({})

  beforeEach(() => {
    ;(useRevealPokerRoundMutation as jest.Mock).mockReturnValue([
      mockReveal,
      { isLoading: false },
    ])
    ;(useResetPokerRoundMutation as jest.Mock).mockReturnValue([
      mockReset,
      { isLoading: false },
    ])
    ;(useSetPokerRoundConsensusMutation as jest.Mock).mockReturnValue([
      mockSetConsensus,
      { isLoading: false },
    ])
  })

  it('renders nothing when canManage is false', () => {
    const { container } = render(
      <VotingActions
        {...defaultProps}
        canManage={false}
        round={makeRound('Voting')}
      />,
    )

    expect(container.firstChild).toBeNull()
  })

  it('shows Reveal Cards button when round is voting', () => {
    render(
      <VotingActions {...defaultProps} round={makeRound('Voting')} />,
    )

    expect(screen.getByText('Reveal Cards')).toBeInTheDocument()
    expect(screen.queryByText('Re-vote')).not.toBeInTheDocument()
  })

  it('shows Re-vote and Accept buttons when round is revealed', () => {
    const round = makeRound('Revealed', {
      votes: [
        {
          id: 'v1',
          participant: { id: 'p1', userName: 'p1', name: 'Alice' },
          value: '5',
          submittedOn: new Date(),
        },
        {
          id: 'v2',
          participant: { id: 'p2', userName: 'p2', name: 'Bob' },
          value: '5',
          submittedOn: new Date(),
        },
      ],
    })

    render(<VotingActions {...defaultProps} round={round} />)

    expect(screen.getByText('Re-vote')).toBeInTheDocument()
    expect(screen.getByText('Accept (5)')).toBeInTheDocument()
    expect(screen.queryByText('Reveal Cards')).not.toBeInTheDocument()
  })

  it('calls reveal mutation when Reveal Cards is clicked', async () => {
    const user = userEvent.setup()

    render(
      <VotingActions {...defaultProps} round={makeRound('Voting')} />,
    )

    await user.click(screen.getByText('Reveal Cards'))

    expect(mockReveal).toHaveBeenCalledWith({
      sessionId: 'session-1',
      roundId: 'round-1',
      sessionKey: 1,
    })
  })

  it('calls reset mutation when Re-vote is clicked', async () => {
    const user = userEvent.setup()
    const round = makeRound('Revealed', {
      votes: [
        {
          id: 'v1',
          participant: { id: 'p1', userName: 'p1', name: 'Alice' },
          value: '5',
          submittedOn: new Date(),
        },
      ],
    })

    render(<VotingActions {...defaultProps} round={round} />)

    await user.click(screen.getByText('Re-vote'))

    expect(mockReset).toHaveBeenCalledWith({
      sessionId: 'session-1',
      roundId: 'round-1',
      sessionKey: 1,
    })
  })

  it('disables Accept button when mode is unavailable', () => {
    const round = makeRound('Revealed', { votes: [] })

    render(<VotingActions {...defaultProps} round={round} />)

    // Accept button should be present but have no mode value
    const acceptButton = screen.getByText('Accept')
    expect(acceptButton.closest('button')).toBeDisabled()
  })
})
