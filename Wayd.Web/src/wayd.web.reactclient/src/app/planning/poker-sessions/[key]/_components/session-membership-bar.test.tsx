import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  PokerSessionDetailsDto,
  PokerSessionMemberDto,
} from '@/src/services/wayd-api'
import SessionMembershipBar from './session-membership-bar'
import {
  useJoinPokerSessionMutation,
  useSetPokerSessionVotingMutation,
} from '@/src/store/features/planning/poker-sessions-api'

jest.mock('@/src/store/features/planning/poker-sessions-api', () => ({
  useJoinPokerSessionMutation: jest.fn(),
  useSetPokerSessionVotingMutation: jest.fn(),
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: jest.fn(), error: jest.fn() }),
}))

const session = { id: 'session-1', key: 7 } as PokerSessionDetailsDto

const member = (isVoting: boolean): PokerSessionMemberDto => ({
  employee: { id: 'employee-1', key: 1, name: 'Avery Chen' },
  isVoting,
  joinedOn: new Date('2026-01-15T10:00:00Z'),
})

describe('SessionMembershipBar', () => {
  const mockJoin = jest.fn().mockResolvedValue({})
  const mockSetVoting = jest.fn().mockResolvedValue({})

  beforeEach(() => {
    jest.clearAllMocks()
    ;(useJoinPokerSessionMutation as jest.Mock).mockReturnValue([
      mockJoin,
      { isLoading: false },
    ])
    ;(useSetPokerSessionVotingMutation as jest.Mock).mockReturnValue([
      mockSetVoting,
      { isLoading: false },
    ])
  })

  it('joins on opening', async () => {
    render(
      <SessionMembershipBar session={session} member={undefined} canJoin />,
    )

    await waitFor(() =>
      expect(mockJoin).toHaveBeenCalledWith({ id: 'session-1', key: 7 }),
    )
  })

  it('does not join a member again', () => {
    render(
      <SessionMembershipBar session={session} member={member(false)} canJoin />,
    )

    expect(mockJoin).not.toHaveBeenCalled()
  })

  it('offers to join after a failed join, without retrying on its own', async () => {
    mockJoin.mockResolvedValueOnce({ error: { status: 500 } })
    const { rerender } = render(
      <SessionMembershipBar session={session} member={undefined} canJoin />,
    )
    await waitFor(() => expect(mockJoin).toHaveBeenCalledTimes(1))

    rerender(
      <SessionMembershipBar session={session} member={undefined} canJoin />,
    )

    expect(mockJoin).toHaveBeenCalledTimes(1)
    await userEvent.click(screen.getByRole('button', { name: 'Join Session' }))
    expect(mockJoin).toHaveBeenCalledTimes(2)
  })

  it('shows whether the member is voting', () => {
    render(
      <SessionMembershipBar session={session} member={member(true)} canJoin />,
    )

    expect(screen.getByRole('switch', { name: 'Voting' })).toBeChecked()
  })

  it('turns voting off', async () => {
    render(
      <SessionMembershipBar session={session} member={member(true)} canJoin />,
    )

    await userEvent.click(screen.getByRole('switch', { name: 'Voting' }))

    expect(mockSetVoting).toHaveBeenCalledWith({
      id: 'session-1',
      key: 7,
      isVoting: false,
    })
  })

  it('explains why an account without an employee cannot join', () => {
    render(
      <SessionMembershipBar
        session={session}
        member={undefined}
        canJoin={false}
      />,
    )

    expect(screen.getByText(/not linked to an employee/)).toBeInTheDocument()
    expect(mockJoin).not.toHaveBeenCalled()
  })
})
