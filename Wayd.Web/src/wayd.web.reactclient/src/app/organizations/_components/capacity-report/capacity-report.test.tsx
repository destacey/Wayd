import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import CapacityReport from './capacity-report'

jest.unmock('dayjs')

const mockReportArgs = jest.fn()
let mockReport: Record<string, unknown> | undefined
jest.mock('@/src/store/features/organization/team-capacity-api', () => ({
  CapacityReportInterval: { Week: 'Week', Month: 'Month' },
  useGetTeamCapacityReportQuery: (arg: unknown) => {
    mockReportArgs(arg)
    return {
      data: mockReport,
      currentData: mockReport,
      isFetching: false,
      error: undefined,
      refetch: jest.fn(),
    }
  },
}))

let mockSprints: unknown[] | undefined
jest.mock('@/src/store/features/organizations/team-api', () => ({
  useGetTeamSprintsQuery: () => ({ data: mockSprints }),
}))

jest.mock('@/src/store/features/planning/planning-interval-api', () => ({
  useGetPlanningIntervalsQuery: () => ({ data: [] }),
  useGetPlanningIntervalIterationsQuery: () => ({ data: [] }),
}))

const mockGrid = jest.fn()
jest.mock('@/src/components/common/wayd-grid', () => ({
  WaydGrid: (props: { csvFileName: string }) => {
    mockGrid(props)
    return <div data-testid={props.csvFileName} />
  },
}))

const measures = (available: number) => ({
  allocatedPersonDays: available + 1,
  holidayPersonDays: 0.5,
  timeAwayPersonDays: 0.5,
  availablePersonDays: available,
  deliveryPersonDays: available,
})

const report = {
  from: '2027-02-01',
  to: '2027-02-28',
  buckets: [],
  workingDayHours: 8,
  total: measures(4.5),
  roles: [],
  teams: [
    {
      nodeId: 0,
      team: { id: 'cards', key: 2, name: 'Cards', code: 'CARDS', type: 'Team' },
      periods: [],
      workingDays: 20,
    },
  ],
  people: [
    {
      employee: { id: 'ada', key: 10, name: 'Ada Lovelace' },
      outsidePersonDays: 3,
      unallocatedPersonDays: 1,
      outsideBuckets: [],
      unallocatedBuckets: [],
    },
  ],
  lines: [
    {
      nodeId: 0,
      employeeId: 'ada',
      countsTowardDeliveryCapacity: true,
      total: measures(4.5),
      buckets: [],
    },
  ],
}

/** The props the main grid was last rendered with. */
const lastGridProps = () =>
  mockGrid.mock.calls
    .map(([props]) => props)
    .filter(
      (p) =>
        p.csvFileName.startsWith('capacity-report-by-') &&
        p.csvFileName !== 'capacity-report-by-role',
    )
    .at(-1)

const lastArgs = () => mockReportArgs.mock.calls.at(-1)![0]

describe('CapacityReport', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockReport = report
    mockSprints = undefined
    localStorage.clear()
  })

  it('asks for the month the team is in, without buckets, and shows the totals', () => {
    // Act
    render(<CapacityReport teamId="cards" teamType="Team" timeZone="UTC" />)

    // Assert
    const args = lastArgs()
    expect(args).toMatchObject({
      teamId: 'cards',
      teamType: 'Team',
      interval: undefined,
    })
    expect(args.from).toMatch(/^\d{4}-\d{2}-01$/)
    expect(
      screen.getByText('Available').closest('.ant-card'),
    ).toHaveTextContent('4.5')
    expect(lastGridProps().csvFileName).toBe('capacity-report-by-team')
    expect(lastGridProps().data[0].name).toBe('Cards')
  })

  it('groups by person with their time outside the report as columns', async () => {
    // Arrange
    render(<CapacityReport teamId="cards" teamType="Team" />)

    // Act
    await userEvent.click(screen.getByText('Person'))

    // Assert
    const props = lastGridProps()
    expect(props.csvFileName).toBe('capacity-report-by-person')
    expect(props.data.map((r: { name: string }) => r.name)).toEqual([
      'Ada Lovelace',
    ])
    expect(props.columns.map((c: { id: string }) => c.id)).toEqual(
      expect.arrayContaining(['outside', 'unallocated']),
    )
  })

  it('asks for weekly buckets when columns are by week', async () => {
    // Arrange
    render(<CapacityReport teamId="cards" teamType="Team" />)

    // Act
    await userEvent.click(screen.getByText('By week'))

    // Assert
    expect(lastArgs().interval).toBe('Week')
  })

  it('shows hours of the system working day when hours are chosen', async () => {
    // Arrange
    render(<CapacityReport teamId="cards" teamType="Team" />)

    // Act
    await userEvent.click(screen.getByText('Hours'))

    // Assert — 4.5 person-days of 8 hours
    const card = screen
      .getByText('Available')
      .closest('.ant-card') as HTMLElement
    expect(within(card).getByText('36')).toBeInTheDocument()
    expect(within(card).getByText('hours')).toBeInTheDocument()
  })

  it('reports over a sprint picked from the team sprints', async () => {
    // Arrange
    mockSprints = [
      { id: 's1', name: 'Sprint 1', start: '2027-01-04', end: '2027-01-17' },
    ]
    render(<CapacityReport teamId="cards" teamType="Team" hasSprints />)

    // Act
    await userEvent.click(screen.getByRole('combobox', { name: 'Sprint' }))
    await userEvent.click(await screen.findByText('Sprint 1'))

    // Assert
    expect(lastArgs()).toMatchObject({ from: '2027-01-04', to: '2027-01-17' })
    const select = screen
      .getByRole('combobox', { name: 'Sprint' })
      .closest('.ant-select') as HTMLElement
    expect(select).toHaveTextContent('Sprint 1')
  })
})
