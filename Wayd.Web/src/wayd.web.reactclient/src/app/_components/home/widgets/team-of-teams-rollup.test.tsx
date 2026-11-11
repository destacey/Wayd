import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import TeamOfTeamsRollup, { rollupHeadline } from './team-of-teams-rollup'
import {
  AttentionStatus,
  Methodology,
  MyTeamOfTeamsRowResponse,
  MyTeamOfTeamsSummaryResponse,
} from '@/src/services/wayd-api'

const mockAuth = { claims: new Set(['Permissions.Iterations.View']) }

vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({
    hasPermissionClaim: (claim: string) => mockAuth.claims.has(claim),
  }),
}))

const NOW = new Date('2026-11-10T12:00:00Z')

beforeEach(() => {
  mockAuth.claims = new Set(['Permissions.Iterations.View'])
})

const team = (name: string, type = 'Team') => ({
  id: name.toLowerCase(),
  key: name.length,
  name,
  code: name.toUpperCase(),
  type,
})

const row = (
  name: string,
  status: AttentionStatus,
  overrides: Partial<MyTeamOfTeamsRowResponse> = {},
): MyTeamOfTeamsRowResponse => ({
  team: team(name),
  isMine: false,
  status,
  ...overrides,
})

const rollup = (
  overrides: Partial<MyTeamOfTeamsSummaryResponse> = {},
): MyTeamOfTeamsSummaryResponse => ({
  team: team('Train', 'Team of Teams'),
  scrumTeamCount: 0,
  kanbanTeamCount: 0,
  rows: [],
  ...overrides,
})

describe('rollupHeadline', () => {
  it('counts the teams and their methodologies', () => {
    // Arrange
    const rows = Array.from({ length: 6 }, (_, i) =>
      row(`T${i}`, AttentionStatus.OnTrack),
    )

    // Act
    const headline = rollupHeadline({
      rows,
      scrumTeamCount: 4,
      kanbanTeamCount: 2,
    })

    // Assert
    expect(headline).toBe('6 teams · 4 Scrum, 2 Kanban')
  })

  it('counts teams of teams in it apart from its teams', () => {
    // Arrange
    const rows = [
      row('Payments', AttentionStatus.OnTrack),
      {
        ...row('Platform', AttentionStatus.OnTrack),
        nested: { teamCount: 4, needsAttentionCount: 0, watchCount: 0 },
      },
    ]

    // Act
    const headline = rollupHeadline({
      rows,
      scrumTeamCount: 1,
      kanbanTeamCount: 0,
    })

    // Assert
    expect(headline).toBe('1 team, 1 team of teams · 1 Scrum')
  })

  it('leaves out a methodology no team follows', () => {
    // Arrange / Act
    const headline = rollupHeadline({
      rows: [row('Solo', AttentionStatus.OnTrack)],
      scrumTeamCount: 0,
      kanbanTeamCount: 1,
    })

    // Assert
    expect(headline).toBe('1 team · 1 Kanban')
  })
})

describe('TeamOfTeamsRollup', () => {
  it("names each row's status for screen readers and on hover", () => {
    // Arrange
    const summary = rollup({
      rows: [
        row('Stuck', AttentionStatus.NeedsAttention),
        row('Slow', AttentionStatus.Watch),
      ],
    })

    // Act
    render(<TeamOfTeamsRollup rollup={summary} now={NOW} />)

    // Assert
    const [first, second] = screen.getAllByRole('listitem')
    expect(
      within(first).getByRole('img', { name: 'Needs attention' }),
    ).toBeInTheDocument()
    expect(
      within(second).getByRole('img', { name: 'Watch' }),
    ).toBeInTheDocument()
  })

  it('says a Scrum team is between sprints only to someone who may see sprints', async () => {
    // Arrange
    const user = userEvent.setup()
    const summary = rollup({
      rows: [
        row('Idle', AttentionStatus.OnTrack, {
          methodology: Methodology.Scrum,
        }),
      ],
    })
    const { unmount } = render(<TeamOfTeamsRollup rollup={summary} now={NOW} />)
    await user.click(screen.getByRole('button', { name: '+ 1 on track' }))
    expect(screen.getByRole('listitem')).toHaveTextContent(
      'IdleNo active sprint',
    )
    unmount()
    mockAuth.claims = new Set()

    // Act
    render(<TeamOfTeamsRollup rollup={summary} now={NOW} />)
    await user.click(screen.getByRole('button', { name: '+ 1 on track' }))

    // Assert
    expect(screen.getByRole('listitem')).toHaveTextContent(/^Idle$/)
  })

  it('collapses every row on track without leaving an empty list, and says whether they are shown', async () => {
    // Arrange
    const user = userEvent.setup()
    const summary = rollup({ rows: [row('Calm', AttentionStatus.OnTrack)] })

    // Act
    render(<TeamOfTeamsRollup rollup={summary} now={NOW} />)

    // Assert
    expect(screen.queryByRole('list')).not.toBeInTheDocument()
    const toggle = screen.getByRole('button', { name: '+ 1 on track' })
    expect(toggle).toHaveAttribute('aria-expanded', 'false')
    await user.click(toggle)
    expect(
      screen.getByRole('button', { name: 'Hide on track' }),
    ).toHaveAttribute('aria-expanded', 'true')
    expect(screen.getAllByRole('listitem')).toHaveLength(1)
  })

  it('lists the teams needing attention and to watch, collapsing those on track', async () => {
    // Arrange
    const user = userEvent.setup()
    const summary = rollup({
      rows: [
        row('Stuck', AttentionStatus.NeedsAttention, {
          methodology: Methodology.Kanban,
          flow: {
            inProgressCount: 5,
            oldestInProgressSince: new Date('2026-11-01T12:00:00Z'),
            medianCycleTimeDays: 2,
          },
        }),
        row('Behind', AttentionStatus.Watch, {
          methodology: Methodology.Scrum,
          sprint: {
            sprint: { id: 's1', key: 41, name: 'Sprint 41' },
            endedNotCompleted: false,
            expectedShare: 0.6,
            doneShare: 0.42,
            status: AttentionStatus.Watch,
          },
        }),
        row('Steady', AttentionStatus.OnTrack, { isMine: true }),
        row('Calm', AttentionStatus.OnTrack),
      ],
    })

    // Act
    render(<TeamOfTeamsRollup rollup={summary} now={NOW} />)

    // Assert
    const list = screen.getByRole('list')
    const items = within(list).getAllByRole('listitem')
    expect(items).toHaveLength(2)
    expect(items[0]).toHaveTextContent('Stuck5 in progress · oldest 9 days')
    expect(items[1]).toHaveTextContent(
      'BehindSprint 41 · 42% done, 60% expected',
    )
    expect(
      within(items[1]).getByRole('link', { name: 'Sprint 41' }),
    ).toHaveAttribute('href', '/work/sprints/41')
    expect(screen.queryByText('Steady')).not.toBeInTheDocument()

    await user.click(screen.getByRole('button', { name: '+ 2 on track' }))

    const expanded = within(list).getAllByRole('listitem')
    expect(expanded).toHaveLength(4)
    expect(expanded[2]).toHaveTextContent('SteadyYour team')
  })

  it('flags a sprint left open past its end', () => {
    // Arrange
    const summary = rollup({
      rows: [
        row('Open', AttentionStatus.NeedsAttention, {
          methodology: Methodology.Scrum,
          sprint: {
            sprint: { id: 's2', key: 9, name: 'Sprint 9' },
            endedNotCompleted: true,
            status: AttentionStatus.NeedsAttention,
          },
        }),
      ],
    })

    // Act
    render(<TeamOfTeamsRollup rollup={summary} now={NOW} />)

    // Assert
    expect(screen.getByRole('listitem')).toHaveTextContent(
      'Sprint 9 ended, not completed',
    )
  })

  it('counts the teams beneath a nested team of teams by status', () => {
    // Arrange
    const summary = rollup({
      rows: [
        {
          ...row('Platform', AttentionStatus.NeedsAttention),
          team: team('Platform', 'Team of Teams'),
          nested: { teamCount: 3, needsAttentionCount: 1, watchCount: 1 },
        },
      ],
    })

    // Act
    render(<TeamOfTeamsRollup rollup={summary} now={NOW} />)

    // Assert
    const item = screen.getByRole('listitem')
    expect(item).toHaveTextContent('3 teams · 1 needs attention, 1 to watch')
    expect(
      within(item).getByRole('link', { name: 'Platform' }),
    ).toHaveAttribute('href', '/organizations/team-of-teams/8')
  })

  it('shows a team with no operating model by name only', async () => {
    // Arrange
    const user = userEvent.setup()
    const summary = rollup({
      rows: [row('Unmodelled', AttentionStatus.OnTrack)],
    })

    // Act
    render(<TeamOfTeamsRollup rollup={summary} now={NOW} />)
    await user.click(screen.getByRole('button', { name: '+ 1 on track' }))

    // Assert
    expect(screen.getByRole('listitem')).toHaveTextContent(/^Unmodelled$/)
  })

  it('links the planning interval and footer to the team of teams page', () => {
    // Arrange
    const summary = rollup({
      planningInterval: {
        planningInterval: { id: 'pi', key: 12, name: 'PI 26.4' },
        objectivesAtRiskCount: 3,
      },
      dependencies: { openCount: 7, atRiskCount: 1, unhealthyCount: 2 },
      risks: { ownOpenCount: 2, teamsOpenCount: 5 },
    })

    // Act
    render(<TeamOfTeamsRollup rollup={summary} now={NOW} />)

    // Assert
    expect(screen.getByRole('link', { name: 'PI 26.4' })).toHaveAttribute(
      'href',
      '/planning/planning-intervals/12',
    )
    expect(screen.getByText('3 objectives at risk')).toBeInTheDocument()
    expect(
      screen.getByRole('link', {
        name: /^7 cross-team dependencies\s*· 3 at risk$/,
      }),
    ).toHaveAttribute(
      'href',
      '/organizations/team-of-teams/5?section=dependencies',
    )
    expect(
      screen.getByRole('link', { name: /^2 open risks\s*· 5 on its teams$/ }),
    ).toHaveAttribute(
      'href',
      '/organizations/team-of-teams/5?section=risk-management',
    )
  })

  it('leaves out each footer section the caller may not see', () => {
    // Arrange / Act
    render(<TeamOfTeamsRollup rollup={rollup()} now={NOW} />)

    // Assert
    expect(screen.queryByText(/dependenc/)).not.toBeInTheDocument()
    expect(screen.queryByText(/risk/)).not.toBeInTheDocument()
    expect(screen.queryByText(/objective/)).not.toBeInTheDocument()
  })
})
