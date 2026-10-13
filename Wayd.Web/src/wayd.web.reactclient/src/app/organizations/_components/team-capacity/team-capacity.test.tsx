import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import TeamCapacity from './team-capacity'

jest.unmock('dayjs')

let mockPermissions: string[] = []
jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({
    hasPermissionClaim: (permission: string) =>
      mockPermissions.includes(permission),
  }),
}))

let mockCapacity: Record<string, unknown> | undefined
// What RTK Query still holds from the previous range while a new one loads.
let mockStaleCapacity: Record<string, unknown> | undefined
let mockIsFetching = false
let mockError: unknown
const mockCapacityArgs = jest.fn()
let mockRollup: Record<string, unknown> | undefined
let mockRollupError: unknown
const mockRollupArgs = jest.fn()
jest.mock('@/src/store/features/organization/team-capacity-api', () => ({
  useGetTeamCapacityQuery: (arg: unknown) => {
    mockCapacityArgs(arg)
    return {
      data: mockCapacity ?? mockStaleCapacity,
      currentData: mockCapacity,
      isLoading: false,
      isFetching: mockIsFetching,
      error: mockError,
      refetch: jest.fn(),
    }
  },
  useGetTeamOfTeamsCapacityRollupQuery: (arg: unknown, options: unknown) => {
    mockRollupArgs(arg, options)
    return {
      currentData: mockRollup,
      isFetching: false,
      error: mockRollupError,
      refetch: jest.fn(),
    }
  },
}))

let mockMembers: unknown[] | undefined

jest.mock('@/src/store/features/organization/team-members-api', () => ({
  useGetTeamMembersQuery: () => ({ data: mockMembers }),
  useGetTeamOfTeamsMembersQuery: () => ({ data: undefined }),
}))

const mockChangeForm = jest.fn()
jest.mock(
  '@/src/app/organizations/_components/allocations/change-allocation-form',
  () => ({
    __esModule: true,
    default: (props: unknown) => {
      mockChangeForm(props)
      return <div data-testid="change-form" />
    },
  }),
)

const mockMoveForm = jest.fn()
jest.mock(
  '@/src/app/organizations/_components/allocations/move-allocation-form',
  () => ({
    __esModule: true,
    default: (props: unknown) => {
      mockMoveForm(props)
      return <div data-testid="move-form" />
    },
  }),
)

const mockInitializeForm = jest.fn()
jest.mock('./initialize-allocations-form', () => ({
  __esModule: true,
  default: (props: unknown) => {
    mockInitializeForm(props)
    return <div data-testid="initialize-form" />
  },
}))

const mockRollupGrid = jest.fn()
jest.mock('./team-capacity-rollup', () => ({
  __esModule: true,
  default: (props: unknown) => {
    mockRollupGrid(props)
    return <div data-testid="rollup-grid" />
  },
}))

const elsewhere = (canManage: boolean) => ({
  id: 'allocation-9',
  team: { id: 'team-2', key: 2, name: 'Loans', code: 'LOANS', type: 'Team' },
  role: undefined,
  start: '2020-01-01',
  end: undefined,
  percent: 50,
  canManage,
})

const capacity = (overrides: Record<string, unknown> = {}) => ({
  from: '2027-02-01',
  to: '2027-02-28',
  workingDays: 20,
  allocatedPersonDays: 10,
  deliveryPersonDays: 10,
  canManage: true,
  roles: [
    {
      role: { id: 'role-1', key: 1, name: 'Engineer' },
      countsTowardDeliveryCapacity: true,
      personDays: 7.5,
    },
    {
      role: { id: 'role-2', key: 2, name: 'Lead' },
      countsTowardDeliveryCapacity: false,
      personDays: 2.5,
    },
  ],
  people: [
    {
      employee: { id: 'employee-1', key: 1, name: 'Ada Lovelace' },
      allocatedPersonDays: 10,
      deliveryPersonDays: 10,
      elsewherePersonDays: 10,
      allocations: [
        {
          id: 'allocation-1',
          role: { id: 'role-1', key: 1, name: 'Engineer' },
          countsTowardDeliveryCapacity: true,
          start: '2020-01-01',
          end: undefined,
          percent: 50,
          personDays: 10,
        },
      ],
      elsewhereAllocations: [elsewhere(true)],
    },
  ],
  ...overrides,
})

const team = { id: 'team-1', name: 'Cards', isActive: true }

const renderSection = (overrides: Partial<typeof team> = {}) =>
  render(<TeamCapacity team={{ ...team, ...overrides }} teamType="Team" />)

const cardsNode = {
  team: { id: 'team-2', key: 2, name: 'Cards', code: 'CARDS', type: 'Team' },
  periods: [{ start: '2027-02-01', end: '2027-02-28' }],
  workingDays: 20,
  allocatedPersonDays: 30,
  deliveryPersonDays: 25,
  directAllocatedPersonDays: 30,
  directDeliveryPersonDays: 25,
  people: [],
  children: [],
}

const rollupOf = (overrides: Record<string, unknown> = {}) => ({
  from: '2027-02-01',
  to: '2027-02-28',
  allocatedPersonDays: 40,
  deliveryPersonDays: 33,
  directAllocatedPersonDays: 10,
  directDeliveryPersonDays: 10,
  people: 4,
  roles: [
    {
      role: { id: 'role-3', key: 3, name: 'Tester' },
      countsTowardDeliveryCapacity: true,
      personDays: 33,
    },
    {
      role: { id: 'role-2', key: 2, name: 'Lead' },
      countsTowardDeliveryCapacity: false,
      personDays: 7,
    },
  ],
  children: [cardsNode],
  ...overrides,
})

const renderTeamOfTeamsSection = () =>
  render(
    <TeamCapacity
      team={{ id: 'tot-1', name: 'Payments', isActive: true }}
      teamType="TeamOfTeams"
    />,
  )

/** The value shown under a summary statistic's title. */
const statistic = (title: string) =>
  screen
    .getByText(title)
    .closest('.ant-statistic')
    ?.querySelector('.ant-statistic-content-value')?.textContent

describe('TeamCapacity', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockPermissions = ['Permissions.Teams.Update']
    mockCapacity = capacity()
    mockStaleCapacity = undefined
    mockIsFetching = false
    mockError = undefined
    mockMembers = [{ employee: { id: 'member-1' } }]
    mockRollup = rollupOf()
    mockRollupError = undefined
  })

  it('asks for the current month by default', () => {
    renderSection()

    const arg = mockCapacityArgs.mock.calls[0][0]
    expect(arg).toMatchObject({ teamId: 'team-1', teamType: 'Team' })
    expect(arg.from).toMatch(/^\d{4}-\d{2}-01$/)
    expect(arg.to.slice(0, 7)).toBe(arg.from.slice(0, 7))
  })

  it('lists each allocation and capacity by role with a non-delivery line', () => {
    renderSection()

    expect(screen.getByRole('link', { name: 'Ada Lovelace' })).toHaveAttribute(
      'href',
      '/organizations/employees/1',
    )
    expect(screen.getByText('50%')).toBeInTheDocument()
    expect(screen.getByText('Non-delivery')).toBeInTheDocument()
    expect(screen.getByText('7.5')).toBeInTheDocument()
    expect(screen.getByText('2.5')).toBeInTheDocument()
  })

  it.each([
    [
      'the caller lacks the Teams Update permission',
      () => (mockPermissions = []),
    ],
    [
      'the caller cannot manage this team',
      () => (mockCapacity = capacity({ canManage: false })),
    ],
  ])('is read-only when %s', (_, arrange) => {
    arrange()

    renderSection()

    expect(
      screen.getByRole('link', { name: 'Ada Lovelace' }),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Change' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Rebalance' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Allocate Person' }),
    ).not.toBeInTheDocument()
  })

  it('is read-only on an inactive team', () => {
    renderSection({ isActive: false })

    expect(
      screen.queryByRole('button', { name: 'Change' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Allocate Person' }),
    ).not.toBeInTheDocument()
  })

  it('opens a change on the allocation as it stands, not cut down to the range', async () => {
    renderSection()

    await userEvent.click(screen.getByRole('button', { name: 'Change' }))

    const props = mockChangeForm.mock.calls[0][0]
    expect(props).toMatchObject({
      employeeId: 'employee-1',
      allocation: {
        teamId: 'team-1',
        roleId: 'role-1',
        start: '2020-01-01',
        percent: 50,
      },
    })
    expect(props.allocation.end).toBeUndefined()
    expect(props.initialDates).toBeUndefined()
  })

  it('shows nothing from the previous range while a new one loads', () => {
    mockStaleCapacity = capacity()
    mockCapacity = undefined
    mockIsFetching = true

    renderSection()

    expect(
      screen.queryByRole('link', { name: 'Ada Lovelace' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Change' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Initialize' }),
    ).not.toBeInTheDocument()
    expect(screen.queryByText('Non-delivery')).not.toBeInTheDocument()
  })

  it('shows the reason and no stale rows when the range fails to load', () => {
    mockStaleCapacity = capacity()
    mockCapacity = undefined
    mockError = {
      status: 400,
      detail: 'The date range cannot be longer than 731 days.',
    }

    renderSection()

    expect(
      screen.getByText('The date range cannot be longer than 731 days.'),
    ).toBeInTheDocument()
    expect(
      screen.queryByRole('link', { name: 'Ada Lovelace' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Change' }),
    ).not.toBeInTheDocument()
  })

  it('rebalances from the other teams the caller can manage, into this team', async () => {
    renderSection()

    await userEvent.click(screen.getByRole('button', { name: 'Rebalance' }))

    expect(mockMoveForm.mock.calls[0][0]).toMatchObject({
      employeeId: 'employee-1',
      allocations: [{ team: { id: 'team-2' } }],
      target: { teamId: 'team-1', roleId: 'role-1' },
    })
  })

  it('offers no rebalance when the caller manages none of the other teams', () => {
    const person = capacity().people[0]
    mockCapacity = capacity({
      people: [{ ...person, elsewhereAllocations: [elsewhere(false)] }],
    })

    renderSection()

    expect(screen.getByRole('button', { name: 'Change' })).toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Rebalance' }),
    ).not.toBeInTheDocument()
  })

  it('ends a new allocation with the range for a non-member and leaves a member open-ended', async () => {
    renderSection()

    await userEvent.click(
      screen.getByRole('button', { name: 'Allocate Person' }),
    )

    const props = mockChangeForm.mock.calls[0][0]
    const { to } = mockCapacityArgs.mock.calls[0][0]
    expect(props.team).toMatchObject({ id: 'team-1' })
    expect(props.endDateFor('someone-else')).toBe(to)
    expect(props.endDateFor('member-1')).toBeUndefined()
  })

  it('offers to initialize only when no one is allocated in the range', async () => {
    renderSection()
    expect(
      screen.queryByRole('button', { name: 'Initialize' }),
    ).not.toBeInTheDocument()

    mockCapacity = capacity({ people: [], roles: [] })
    renderSection()
    await userEvent.click(screen.getByRole('button', { name: 'Initialize' }))

    expect(mockInitializeForm.mock.calls[0][0]).toMatchObject({
      teamId: 'team-1',
      teamType: 'Team',
      from: mockCapacityArgs.mock.calls[0][0].from,
    })
  })

  it('does not offer to initialize to a caller who cannot manage the team', () => {
    mockCapacity = capacity({ people: [], roles: [], canManage: false })

    renderSection()

    expect(
      screen.queryByRole('button', { name: 'Initialize' }),
    ).not.toBeInTheDocument()
  })

  it('offers no end date default until the members have loaded', async () => {
    mockMembers = undefined
    renderSection()

    await userEvent.click(
      screen.getByRole('button', { name: 'Allocate Person' }),
    )

    expect(mockChangeForm.mock.calls[0][0].endDateFor).toBeUndefined()
  })

  describe('on a team', () => {
    it('does not read a rollup and totals its own allocations', () => {
      renderSection()

      expect(mockRollupArgs.mock.calls[0][1]).toEqual({ skip: true })
      expect(statistic('People')).toBe('1')
      expect(statistic('Allocated person-days')).toBe('10')
      expect(screen.queryByTestId('rollup-grid')).not.toBeInTheDocument()
      expect(screen.queryByText('Direct allocations')).not.toBeInTheDocument()
    })
  })

  describe('on a team of teams', () => {
    it('reads the rollup for the same range as its direct allocations', () => {
      renderTeamOfTeamsSection()

      const range = mockCapacityArgs.mock.calls[0][0]
      expect(mockRollupArgs.mock.calls[0]).toEqual([
        { teamId: 'tot-1', from: range.from, to: range.to },
        { skip: false },
      ])
    })

    it('totals the teams beneath it with its direct allocations', () => {
      renderTeamOfTeamsSection()

      expect(statistic('People')).toBe('4')
      expect(statistic('Allocated person-days')).toBe('40')
      expect(statistic('Delivery person-days')).toBe('33')
      expect(statistic('Working days (direct allocations)')).toBe('20')
    })

    it('breaks the whole rollup down by role', () => {
      renderTeamOfTeamsSection()

      expect(screen.getByText('Tester')).toBeInTheDocument()
      expect(screen.getByText('Non-delivery')).toBeInTheDocument()
      expect(screen.getByText('7')).toBeInTheDocument()
      expect(screen.queryByText('7.5')).not.toBeInTheDocument()
    })

    it('keeps its direct allocations editable above the teams beneath it', () => {
      renderTeamOfTeamsSection()

      expect(screen.getByText('Direct allocations')).toBeInTheDocument()
      expect(screen.getByRole('button', { name: 'Change' })).toBeInTheDocument()
      expect(mockRollupGrid.mock.calls[0][0]).toMatchObject({
        nodes: [cardsNode],
        range: {
          from: mockCapacityArgs.mock.calls[0][0].from,
          to: mockCapacityArgs.mock.calls[0][0].to,
        },
      })
    })

    it('still offers to initialize when only the teams beneath it have capacity', () => {
      mockCapacity = capacity({ people: [], roles: [] })

      renderTeamOfTeamsSection()

      expect(
        screen.getByRole('button', { name: 'Initialize' }),
      ).toBeInTheDocument()
    })

    it('shows the reason when the rollup fails to load', () => {
      mockRollup = undefined
      mockRollupError = { detail: 'The date range is too long.' }

      renderTeamOfTeamsSection()

      expect(
        screen.getByText('The date range is too long.'),
      ).toBeInTheDocument()
      expect(
        screen.queryByText('Allocated person-days'),
      ).not.toBeInTheDocument()
    })
  })
})
