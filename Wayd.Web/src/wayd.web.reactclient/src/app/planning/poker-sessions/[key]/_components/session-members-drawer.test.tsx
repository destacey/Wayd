import type { Mock } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { PokerSessionDetailsDto, Visibility } from '@/src/services/wayd-api'
import SessionMembersDrawer from './session-members-drawer'
import { useGetPokerSessionInvitedTeamMembersQuery } from '@/src/store/features/planning/poker-sessions-api'

vi.mock('@/src/store/features/planning/poker-sessions-api', () => ({
  useGetPokerSessionInvitedTeamMembersQuery: vi.fn(),
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
      employee: employee('f-1', 'Fran Facilitator'),
      isVoting: true,
      joinedOn: new Date('2026-01-15T10:00:00Z'),
    },
    {
      employee: employee('m-2', 'Pat Product'),
      isVoting: false,
      joinedOn: new Date('2026-01-15T10:01:00Z'),
    },
  ],
} as unknown as PokerSessionDetailsDto

describe('SessionMembersDrawer', () => {
  beforeEach(() => {
    ;(useGetPokerSessionInvitedTeamMembersQuery as Mock).mockReturnValue({
      data: [employee('t-1', 'Tam Teammate')],
      isFetching: false,
    })
  })

  it('lists facilitators, and who joined with their tags', () => {
    render(<SessionMembersDrawer session={session} open onClose={vi.fn()} />)

    expect(screen.getByText('Joined (2)')).toBeInTheDocument()
    expect(screen.getByText('Pat Product')).toBeInTheDocument()
    expect(screen.getAllByText('Fran Facilitator')).toHaveLength(2)
    expect(screen.getByText('Facilitator')).toBeInTheDocument()
    expect(screen.getAllByText('Voting')).toHaveLength(1)
  })

  it('expands an invited team into its members', async () => {
    render(<SessionMembersDrawer session={session} open onClose={vi.fn()} />)

    expect(useGetPokerSessionInvitedTeamMembersQuery).toHaveBeenLastCalledWith(
      { sessionId: 'session-1', teamId: 'team-1' },
      { skip: true },
    )
    expect(screen.queryByText('Tam Teammate')).not.toBeInTheDocument()

    const team = screen.getByRole('button', { name: /Platform/ })
    expect(team).toHaveAttribute('aria-expanded', 'false')

    await userEvent.click(team)

    expect(useGetPokerSessionInvitedTeamMembersQuery).toHaveBeenLastCalledWith(
      { sessionId: 'session-1', teamId: 'team-1' },
      { skip: false },
    )
    expect(team).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getByText('Tam Teammate')).toBeInTheDocument()
    expect(screen.getByText('Ivy Invited')).toBeInTheDocument()
  })

  it('says so when a team’s members cannot be loaded', async () => {
    ;(useGetPokerSessionInvitedTeamMembersQuery as Mock).mockReturnValue({
      data: undefined,
      isFetching: false,
      isError: true,
    })
    render(<SessionMembersDrawer session={session} open onClose={vi.fn()} />)

    await userEvent.click(screen.getByRole('button', { name: /Platform/ }))

    expect(
      screen.getByText("Could not load this team's members."),
    ).toBeInTheDocument()
    expect(screen.queryByText('No members.')).not.toBeInTheDocument()
  })

  it('hides the invites of a public session, which admit no one', () => {
    render(
      <SessionMembersDrawer
        session={{ ...session, visibility: Visibility.Public }}
        open
        onClose={vi.fn()}
      />,
    )

    expect(screen.queryByText('Ivy Invited')).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: /Platform/ }),
    ).not.toBeInTheDocument()
  })
})
