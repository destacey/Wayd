import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  PokerSessionDetailsDto,
  PokerSessionMemberMode,
  Visibility,
} from '@/src/services/wayd-api'
import SessionMembersDrawer from './session-members-drawer'
import { useGetPokerSessionInvitedTeamMembersQuery } from '@/src/store/features/planning/poker-sessions-api'

jest.mock('@/src/store/features/planning/poker-sessions-api', () => ({
  useGetPokerSessionInvitedTeamMembersQuery: jest.fn(),
}))

const employee = (id: string, name: string) => ({ id, key: 1, name })

const session = {
  id: 'session-1',
  key: 7,
  visibility: Visibility.Private,
  facilitators: [employee('f-1', 'Fran Facilitator')],
  invitedEmployees: [employee('i-1', 'Ivy Invited')],
  invitedTeams: [
    { id: 'team-1', key: 1, name: 'Platform', code: 'PLT', type: 'Team' },
  ],
  members: [
    {
      employee: employee('m-1', 'Pat Participant'),
      mode: PokerSessionMemberMode.Participant,
      joinedOn: new Date('2026-01-15T10:00:00Z'),
    },
    {
      employee: employee('m-2', 'Oli Observer'),
      mode: PokerSessionMemberMode.Observer,
      joinedOn: new Date('2026-01-15T10:01:00Z'),
    },
    {
      employee: employee('m-3', 'Lee Left'),
      mode: PokerSessionMemberMode.Participant,
      joinedOn: new Date('2026-01-15T10:02:00Z'),
      leftOn: new Date('2026-01-15T10:30:00Z'),
    },
  ],
} as unknown as PokerSessionDetailsDto

describe('SessionMembersDrawer', () => {
  beforeEach(() => {
    ;(useGetPokerSessionInvitedTeamMembersQuery as jest.Mock).mockReturnValue({
      data: [employee('t-1', 'Tam Teammate')],
      isFetching: false,
    })
  })

  it('lists facilitators, members by mode, and who has left', () => {
    render(<SessionMembersDrawer session={session} open onClose={jest.fn()} />)

    expect(screen.getByText('Fran Facilitator')).toBeInTheDocument()
    expect(screen.getByText('In the session (2)')).toBeInTheDocument()
    expect(screen.getByText('Pat Participant')).toBeInTheDocument()
    expect(screen.getByText('Oli Observer')).toBeInTheDocument()
    expect(screen.getByText('Left (1)')).toBeInTheDocument()
    expect(screen.getByText('Lee Left')).toBeInTheDocument()
  })

  it('expands an invited team into its members, each marked via the team', async () => {
    render(<SessionMembersDrawer session={session} open onClose={jest.fn()} />)

    expect(useGetPokerSessionInvitedTeamMembersQuery).toHaveBeenLastCalledWith(
      { sessionId: 'session-1', teamId: 'team-1' },
      { skip: true },
    )
    expect(screen.queryByText('Tam Teammate')).not.toBeInTheDocument()

    await userEvent.click(screen.getByText(/Platform/))

    expect(useGetPokerSessionInvitedTeamMembersQuery).toHaveBeenLastCalledWith(
      { sessionId: 'session-1', teamId: 'team-1' },
      { skip: false },
    )
    expect(screen.getByText('Tam Teammate')).toBeInTheDocument()
    expect(screen.getByText('via Platform')).toBeInTheDocument()
    expect(screen.getByText('Ivy Invited')).toBeInTheDocument()
  })
})
