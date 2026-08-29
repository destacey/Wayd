import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import PokerLobbyState from './poker-lobby-state'
import { useAddPokerRoundMutation } from '@/src/store/features/planning/poker-sessions-api'

jest.mock('@/src/store/features/planning/poker-sessions-api', () => ({
  useAddPokerRoundMutation: jest.fn(),
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({
    success: jest.fn(),
    error: jest.fn(),
  }),
}))

const defaultProps = {
  isActive: true,
  canManage: true,
  sessionId: 'session-1',
  sessionKey: 1,
}

describe('PokerLobbyState', () => {
  const mockAddRound = jest.fn().mockResolvedValue({})

  beforeEach(() => {
    ;(useAddPokerRoundMutation as jest.Mock).mockReturnValue([
      mockAddRound,
      { isLoading: false },
    ])
  })

  describe('when session is active', () => {
    it('displays "Ready to estimate" heading', () => {
      render(<PokerLobbyState {...defaultProps} />)

      expect(screen.getByText('Ready to estimate')).toBeInTheDocument()
    })

    it('shows input and Start button when user can manage', () => {
      render(<PokerLobbyState {...defaultProps} canManage={true} />)

      expect(
        screen.getByPlaceholderText('Work item ID or label (optional)'),
      ).toBeInTheDocument()
      expect(screen.getByText('Start')).toBeInTheDocument()
    })

    it('shows waiting message when user cannot manage', () => {
      render(<PokerLobbyState {...defaultProps} canManage={false} />)

      expect(
        screen.getByText('Waiting for the facilitator to start a round.'),
      ).toBeInTheDocument()
      expect(screen.queryByText('Start')).not.toBeInTheDocument()
    })

    it('calls addRound when Start is clicked', async () => {
      const user = userEvent.setup()
      render(<PokerLobbyState {...defaultProps} />)

      const input = screen.getByPlaceholderText(
        'Work item ID or label (optional)',
      )
      await user.type(input, 'ITEM-123')
      await user.click(screen.getByText('Start'))

      expect(mockAddRound).toHaveBeenCalledWith({
        sessionId: 'session-1',
        sessionKey: 1,
        request: { label: 'ITEM-123' },
      })
    })

    it('sends undefined label when input is empty', async () => {
      const user = userEvent.setup()
      render(<PokerLobbyState {...defaultProps} />)

      await user.click(screen.getByText('Start'))

      expect(mockAddRound).toHaveBeenCalledWith({
        sessionId: 'session-1',
        sessionKey: 1,
        request: { label: undefined },
      })
    })

    it('renders decorative lobby cards', () => {
      const { container } = render(<PokerLobbyState {...defaultProps} />)

      // lobbyCards is the container, lobbyCard is each individual card
      const lobbyCardsContainer = container.querySelector('[class*="lobbyCards"]')
      expect(lobbyCardsContainer).toBeInTheDocument()
      expect(lobbyCardsContainer!.children.length).toBe(3)
    })
  })

  describe('when session is completed', () => {
    it('displays "Session Completed" heading', () => {
      render(<PokerLobbyState {...defaultProps} isActive={false} />)

      expect(screen.getByText('Session Completed')).toBeInTheDocument()
    })

    it('displays no-rounds message', () => {
      render(<PokerLobbyState {...defaultProps} isActive={false} />)

      expect(
        screen.getByText('No rounds were created in this session.'),
      ).toBeInTheDocument()
    })

    it('does not show Start button or input', () => {
      render(<PokerLobbyState {...defaultProps} isActive={false} />)

      expect(screen.queryByText('Start')).not.toBeInTheDocument()
      expect(
        screen.queryByPlaceholderText('Work item ID or label (optional)'),
      ).not.toBeInTheDocument()
    })

    it('does not render decorative lobby cards', () => {
      const { container } = render(
        <PokerLobbyState {...defaultProps} isActive={false} />,
      )

      const lobbyCards = container.querySelectorAll('[class*="lobbyCard"]')
      expect(lobbyCards.length).toBe(0)
    })
  })
})
