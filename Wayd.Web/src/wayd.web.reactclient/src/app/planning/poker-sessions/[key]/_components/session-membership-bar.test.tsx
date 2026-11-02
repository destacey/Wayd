import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  PokerSessionDetailsDto,
  PokerSessionMemberDto,
  PokerSessionMemberMode,
} from '@/src/services/wayd-api'
import SessionMembershipBar from './session-membership-bar'
import {
  useChangePokerSessionModeMutation,
  useJoinPokerSessionMutation,
  useLeavePokerSessionMutation,
} from '@/src/store/features/planning/poker-sessions-api'

jest.mock('@/src/store/features/planning/poker-sessions-api', () => ({
  useJoinPokerSessionMutation: jest.fn(),
  useChangePokerSessionModeMutation: jest.fn(),
  useLeavePokerSessionMutation: jest.fn(),
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: jest.fn(), error: jest.fn() }),
}))

const session = { id: 'session-1', key: 7 } as PokerSessionDetailsDto

const member = (mode: PokerSessionMemberMode): PokerSessionMemberDto => ({
  employee: { id: 'employee-1', key: 1, name: 'Avery Chen' },
  mode,
  joinedOn: new Date('2026-01-15T10:00:00Z'),
})

describe('SessionMembershipBar', () => {
  const mockJoin = jest.fn().mockResolvedValue({})
  const mockChangeMode = jest.fn().mockResolvedValue({})
  const mockLeave = jest.fn().mockResolvedValue({})

  beforeEach(() => {
    jest.clearAllMocks()
    ;(useJoinPokerSessionMutation as jest.Mock).mockReturnValue([
      mockJoin,
      { isLoading: false },
    ])
    ;(useChangePokerSessionModeMutation as jest.Mock).mockReturnValue([
      mockChangeMode,
      { isLoading: false },
    ])
    ;(useLeavePokerSessionMutation as jest.Mock).mockReturnValue([
      mockLeave,
      { isLoading: false },
    ])
  })

  it('joins as a Participant by default', async () => {
    render(
      <SessionMembershipBar session={session} member={undefined} canJoin />,
    )

    await userEvent.click(screen.getByRole('button', { name: 'Join Session' }))

    expect(mockJoin).toHaveBeenCalledWith({
      id: 'session-1',
      key: 7,
      mode: PokerSessionMemberMode.Participant,
    })
  })

  it('joins as an Observer when chosen', async () => {
    render(
      <SessionMembershipBar session={session} member={undefined} canJoin />,
    )

    await userEvent.click(screen.getByText('Observer'))
    await userEvent.click(screen.getByRole('button', { name: 'Join Session' }))

    expect(mockJoin).toHaveBeenCalledWith(
      expect.objectContaining({ mode: PokerSessionMemberMode.Observer }),
    )
  })

  it('switches a member to Observer', async () => {
    render(
      <SessionMembershipBar
        session={session}
        member={member(PokerSessionMemberMode.Participant)}
        canJoin
      />,
    )

    await userEvent.click(screen.getByText('Observer'))

    expect(mockChangeMode).toHaveBeenCalledWith({
      id: 'session-1',
      key: 7,
      mode: PokerSessionMemberMode.Observer,
    })
    expect(
      screen.queryByRole('button', { name: 'Join Session' }),
    ).not.toBeInTheDocument()
  })

  it('leaves the session', async () => {
    render(
      <SessionMembershipBar
        session={session}
        member={member(PokerSessionMemberMode.Observer)}
        canJoin
      />,
    )

    await userEvent.click(screen.getByRole('button', { name: 'Leave' }))

    expect(mockLeave).toHaveBeenCalledWith({ id: 'session-1', key: 7 })
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
    expect(
      screen.queryByRole('button', { name: 'Join Session' }),
    ).not.toBeInTheDocument()
  })
})
