import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import dayjs from 'dayjs'
import {
  DependencyDto,
  TeamDependencyAnalysisDto,
} from '@/src/services/wayd-api'
import { useGetTeamDependencyAnalysisQuery } from '@/src/store/features/organizations/team-api'
import { addCalendarDays, calendarDateInZone } from '@/src/utils/calendar-date'
import {
  DependencyAnalysisReport,
  DependencyAnalysisReportView,
} from './dependency-analysis-report'

vi.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamDependencyAnalysisQuery: vi.fn(),
}))

const mockWheelMounts = vi.fn()

// What each explorer does with the dependencies is its own suite's concern; here, only which one is shown,
// and when it is mounted afresh.
vi.mock('@/src/components/common/dependency-wheel', async () => {
  const { useEffect } = await vi.importActual<typeof import('react')>('react')
  const MockWheel = (props: { dependencies: DependencyDto[] }) => {
    useEffect(() => {
      mockWheelMounts()
    }, [])
    return (
      <div aria-label="wheel">{props.dependencies.length} on the wheel</div>
    )
  }
  return { DependencyWheelExplorer: MockWheel }
})
vi.mock('@/src/components/common/work/dependency-partners', () => ({
  DependencyPartnersExplorer: (props: {
    teamId: string
    dependencies: DependencyDto[]
  }) => (
    <div aria-label="partners">
      {props.dependencies.length} with partners of {props.teamId}
    </div>
  ),
}))
vi.mock('./dependency-analysis-over-time', () => ({
  default: (props: { periods: unknown[] }) => (
    <div aria-label="over time">{props.periods.length} periods</div>
  ),
}))

const team = (id: string, name: string, code: string) => ({
  id,
  key: 1,
  name,
  code,
  type: 'Team',
})
const payments = team('pay', 'Payments', 'PAY')
const data = team('dat', 'Data', 'DAT')

const createAnalysis = (
  overrides: Partial<TeamDependencyAnalysisDto> = {},
): TeamDependencyAnalysisDto =>
  ({
    team: { ...team('root', 'Platform ART', 'ART'), type: 'Team of Teams' },
    from: '2026-07-01',
    to: '2026-09-28',
    timeZone: 'America/New_York',
    summary: {
      dependencies: 7,
      created: 4,
      closed: 3,
      openAtEnd: 5,
      predecessorRemoved: 2,
      teamsInvolved: 6,
      teamsOutside: 2,
    },
    pairs: [
      {
        predecessorTeam: payments,
        successorTeam: data,
        predecessorTeamInScope: true,
        successorTeamInScope: false,
        dependencies: 7,
        created: 4,
        closed: 3,
        openAtEnd: 5,
      },
    ],
    periods: [
      {
        start: '2026-07-01',
        end: '2026-07-14',
        created: 4,
        closed: 3,
        open: 5,
      },
    ],
    // The first two have a removed predecessor, as predecessorRemoved says.
    dependencies: Array.from({ length: 7 }, (_, i) => ({
      id: `d${i}`,
      source: {
        statusCategory:
          i < 2 ? { id: 3, name: 'Removed' } : { id: 1, name: 'Active' },
      },
    })),
    ...overrides,
  }) as TeamDependencyAnalysisDto

const julyToSeptember: [dayjs.Dayjs, dayjs.Dayjs] = [
  dayjs('2026-07-01'),
  dayjs('2026-09-28'),
]

const view = (
  analysis: TeamDependencyAnalysisDto | undefined,
  isTeamOfTeams = true,
  error?: unknown,
  range = julyToSeptember,
  refetch = vi.fn(),
) => (
  <DependencyAnalysisReportView
    analysis={analysis}
    isLoading={false}
    error={error}
    refetch={refetch}
    range={range}
    onRangeChange={vi.fn()}
    isTeamOfTeams={isTeamOfTeams}
  />
)

const renderView = (
  analysis: TeamDependencyAnalysisDto | undefined,
  isTeamOfTeams = true,
  error?: unknown,
) => render(view(analysis, isTeamOfTeams, error))

/** The card's value, read as the one element in the card whose whole text it is. */
const expectCard = (title: string, value: string) => {
  const card = screen.getByText(title).closest('.ant-card') as HTMLElement
  expect(within(card).getByText(value, { exact: true })).toBeInTheDocument()
}

beforeEach(() => {
  mockWheelMounts.mockClear()
})

describe('DependencyAnalysisReportView', () => {
  it('shows the summary for the range', () => {
    // Act
    renderView(createAnalysis())

    // Assert
    expectCard('Dependencies', '7')
    expectCard('Created', '4')
    expectCard('Closed', '3')
    expectCard('Open at End', '5')
    expectCard('Predecessor Removed', '2')
    expectCard('Teams Involved', '6')
    expectCard('Teams Outside', '2')
  })

  it('names the time zone the days are counted in', () => {
    // Act
    renderView(createAnalysis())

    // Assert
    expect(
      screen.getByText(
        /whole days in the team of teams' time zone, America\/New_York at the end of the range/,
      ),
    ).toBeInTheDocument()
  })

  it('draws a team of teams on the wheel', () => {
    // Act
    renderView(createAnalysis())

    // Assert
    expect(screen.getByLabelText('wheel')).toHaveTextContent('7 on the wheel')
    expect(screen.queryByLabelText('partners')).not.toBeInTheDocument()
  })

  it('narrows the wheel to dependencies whose predecessor was removed once asked', async () => {
    // Arrange
    const user = userEvent.setup()
    renderView(createAnalysis())

    // Act
    await user.click(
      screen.getByLabelText('Only dependencies whose predecessor was removed'),
    )

    // Assert
    expect(screen.getByLabelText('wheel')).toHaveTextContent('2 on the wheel')
  })

  it("narrows a team's partners the same way", async () => {
    // Arrange
    const user = userEvent.setup()
    renderView(createAnalysis(), false)

    // Act
    await user.click(
      screen.getByLabelText('Only dependencies whose predecessor was removed'),
    )

    // Assert
    expect(screen.getByLabelText('partners')).toHaveTextContent(
      '2 with partners of root',
    )
  })

  it('shows every dependency again once the filter is unticked', async () => {
    // Arrange
    const user = userEvent.setup()
    renderView(createAnalysis())
    const filter = screen.getByLabelText(
      'Only dependencies whose predecessor was removed',
    )
    await user.click(filter)

    // Act
    await user.click(filter)

    // Assert
    expect(screen.getByLabelText('wheel')).toHaveTextContent('7 on the wheel')
  })

  it('shows every dependency for a range with none whose predecessor was removed, even with the filter ticked', async () => {
    // Arrange
    const user = userEvent.setup()
    const { rerender } = render(view(createAnalysis()))
    await user.click(
      screen.getByLabelText('Only dependencies whose predecessor was removed'),
    )

    // Act
    rerender(
      view(
        createAnalysis({
          summary: { ...createAnalysis().summary, predecessorRemoved: 0 },
        }),
      ),
    )

    // Assert
    expect(screen.getByLabelText('wheel')).toHaveTextContent('7 on the wheel')
  })

  it('offers no removed-predecessor filter when there are none', () => {
    // Act
    renderView(
      createAnalysis({
        summary: { ...createAnalysis().summary, predecessorRemoved: 0 },
      }),
    )

    // Assert
    expect(
      screen.queryByLabelText(
        'Only dependencies whose predecessor was removed',
      ),
    ).not.toBeInTheDocument()
  })

  it("draws a team's partners and calls the outside teams partner teams", () => {
    // Act
    renderView(createAnalysis(), false)

    // Assert
    expect(screen.getByLabelText('partners')).toHaveTextContent(
      '7 with partners of root',
    )
    expect(screen.queryByLabelText('wheel')).not.toBeInTheDocument()
    expectCard('Partner Teams', '2')
    expect(screen.queryByText('Teams Outside')).not.toBeInTheDocument()
  })

  it('mounts the wheel afresh when the range changes, so a pick on the old range is dropped', () => {
    // Arrange
    const analysis = createAnalysis()
    const { rerender } = render(view(analysis))

    // Act
    rerender(view(analysis))
    rerender(
      view(analysis, true, undefined, [
        dayjs('2026-08-01'),
        dayjs('2026-09-28'),
      ]),
    )

    // Assert
    expect(mockWheelMounts).toHaveBeenCalledTimes(2)
  })

  it('lists the team pairs, marking teams outside a team of teams', async () => {
    // Arrange
    const user = userEvent.setup()
    renderView(createAnalysis())

    // Act
    await user.click(screen.getByRole('tab', { name: 'Team Pairs' }))

    // Assert
    const link = screen.getByRole('link', { name: 'Payments' })
    expect(link).toHaveAttribute('href', '/organizations/teams/1')
    const cells = within(link.closest('tr')!).getAllByRole('gridcell')
    expect(cells.map((c) => c.textContent)).toEqual([
      'Payments',
      'DataOutside',
      '7',
      '4',
      '3',
      '5',
    ])
  })

  it("doesn't mark partners outside for one team, since every partner is", async () => {
    // Arrange
    const user = userEvent.setup()
    renderView(createAnalysis(), false)

    // Act
    await user.click(screen.getByRole('tab', { name: 'Team Pairs' }))

    // Assert
    expect(screen.queryByText('Outside')).not.toBeInTheDocument()
  })

  it('shows the periods over time', async () => {
    // Arrange
    const user = userEvent.setup()
    renderView(createAnalysis())

    // Act
    await user.click(screen.getByRole('tab', { name: 'Over Time' }))

    // Assert
    expect(screen.getByLabelText('over time')).toHaveTextContent('1 periods')
  })

  it('says so when no dependency crossed team lines in the range', () => {
    // Act
    renderView(
      createAnalysis({
        summary: {
          dependencies: 0,
          created: 0,
          closed: 0,
          openAtEnd: 0,
          predecessorRemoved: 0,
          teamsInvolved: 0,
          teamsOutside: 0,
        },
        pairs: [],
        dependencies: [],
      }),
    )

    // Assert
    expect(
      screen.getByText(
        'No dependencies crossed team lines in this date range.',
      ),
    ).toBeInTheDocument()
    expect(screen.queryByLabelText('wheel')).not.toBeInTheDocument()
  })

  it('retries a failed request', async () => {
    // Arrange
    const user = userEvent.setup()
    const refetch = vi.fn()
    render(view(undefined, true, { status: 500 }, julyToSeptember, refetch))

    // Act
    await user.click(screen.getByRole('button', { name: 'Retry' }))

    // Assert
    expect(refetch).toHaveBeenCalledTimes(1)
  })

  it("shows the API's validation messages when the request is rejected", () => {
    // Act
    renderView(undefined, true, {
      status: 422,
      title: 'Validation failed',
      errors: { '': ['The date range must be at most 731 days.'] },
    })

    // Assert
    expect(
      screen.getByText('The dependency analysis could not be loaded.'),
    ).toBeInTheDocument()
    expect(
      screen.getByText('The date range must be at most 731 days.'),
    ).toBeInTheDocument()
  })
})

describe('DependencyAnalysisReport', () => {
  it('asks for the last 90 days ending today in UTC without a zone, as the API counts it', () => {
    // Arrange
    const today = calendarDateInZone(new Date(), 'UTC')
    const query = vi.mocked(useGetTeamDependencyAnalysisQuery)
    query.mockReturnValue({
      data: undefined,
      isFetching: true,
      error: undefined,
      refetch: vi.fn(),
    } as unknown as ReturnType<typeof useGetTeamDependencyAnalysisQuery>)

    // Act
    render(<DependencyAnalysisReport teamType="team-of-teams" teamCode="ART" />)

    // Assert
    expect(query).toHaveBeenCalledWith({
      teamType: 'team-of-teams',
      teamIdOrCode: 'ART',
      from: addCalendarDays(today, -89),
      to: today,
    })
  })

  it.each([
    ['Pacific/Kiritimati', '2026-06-26', '2026-09-23'],
    ['Etc/GMT+12', '2026-06-24', '2026-09-21'],
  ])(
    'ends the default range on today in the team time zone %s',
    (timeZone, from, to) => {
      // Arrange
      vi.useFakeTimers({ now: new Date('2026-09-22T11:00:00Z') })
      const query = vi.mocked(useGetTeamDependencyAnalysisQuery)
      query.mockReturnValue({
        data: undefined,
        isFetching: true,
        error: undefined,
        refetch: vi.fn(),
      } as unknown as ReturnType<typeof useGetTeamDependencyAnalysisQuery>)

      try {
        // Act
        render(
          <DependencyAnalysisReport
            teamType="team"
            teamCode="PAY"
            timeZone={timeZone}
          />,
        )

        // Assert
        expect(query).toHaveBeenLastCalledWith({
          teamType: 'team',
          teamIdOrCode: 'PAY',
          from,
          to,
        })
      } finally {
        vi.useRealTimers()
      }
    },
  )
})
