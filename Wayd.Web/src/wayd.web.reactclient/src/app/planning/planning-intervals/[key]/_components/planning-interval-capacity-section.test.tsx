import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import {
  PlanningIntervalCapacityDto,
  PlanningIntervalTeamCapacityDto,
} from '@/src/services/wayd-api'
import PlanningIntervalCapacitySection, {
  capacityText,
  teamHref,
} from './planning-interval-capacity-section'

jest.unmock('dayjs')

// The antd Table measures its scrollbar with a pseudo-element, which jsdom does
// not implement and reports as an error.
Object.defineProperty(window, 'getComputedStyle', {
  value: () => ({
    getPropertyValue: () => '',
  }),
})

let mockPermissions: string[] = []
jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({
    hasPermissionClaim: (permission: string) =>
      mockPermissions.includes(permission),
  }),
}))

let mockCapacity: PlanningIntervalCapacityDto | undefined
jest.mock('@/src/store/features/planning/planning-interval-api', () => ({
  useGetPlanningIntervalCapacityQuery: () => ({
    data: mockCapacity,
    isFetching: false,
    error: undefined,
  }),
}))

const mockPlan = jest.fn()
jest.mock(
  '@/src/app/organizations/_components/capacity-plan/team-capacity-plan',
  () => ({
    __esModule: true,
    default: (props: unknown) => {
      mockPlan(props)
      return <div data-testid="team-plan" />
    },
  }),
)

const figures = (delivery: number) => ({
  allocatedPersonDays: delivery,
  deliveryPersonDays: delivery,
  awayPersonDays: 0,
})

const team: PlanningIntervalTeamCapacityDto = {
  team: { id: 't1', key: 14, name: 'Team Atlas', code: 'AT', type: 'Team' },
  isActive: true,
  hasAllocations: true,
  total: figures(60),
  iterations: [figures(25.5), figures(34.5)],
  teamDaysOff: ['2026-01-07'],
}

const group: PlanningIntervalTeamCapacityDto = {
  team: {
    id: 'g1',
    key: 3,
    name: 'Group Beacon',
    code: 'GB',
    type: 'Team of Teams',
  },
  isActive: true,
  hasAllocations: false,
  total: figures(0),
  iterations: [figures(0), figures(0)],
  teamDaysOff: [],
}

const capacity: PlanningIntervalCapacityDto = {
  start: '2026-01-05',
  end: '2026-03-01',
  iterations: [
    {
      id: 'i1',
      key: 1,
      name: 'Iteration 1',
      category: { id: 1, name: 'Development' },
      start: '2026-01-05',
      end: '2026-02-01',
    },
    {
      id: 'i2',
      key: 2,
      name: 'Iteration 2',
      category: { id: 2, name: 'IP' },
      start: '2026-02-02',
      end: '2026-03-01',
    },
  ],
  teams: [team, group],
}

describe('capacityText and teamHref', () => {
  it('reads delivery person-days, or a dash for a team with no one allocated', () => {
    // Arrange / Act / Assert
    expect(capacityText(team, team.iterations[0])).toBe('25.5')
    expect(capacityText(group, group.total)).toBe('—')
  })

  it('links a team and a team of teams to their own pages', () => {
    // Arrange / Act / Assert
    expect(teamHref(team.team)).toBe('/organizations/teams/14')
    expect(teamHref(group.team)).toBe('/organizations/team-of-teams/3')
  })
})

describe('PlanningIntervalCapacitySection', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockPermissions = ['Permissions.Teams.View']
    mockCapacity = capacity
  })

  it('shows each team against the iterations, with the IP iteration marked', () => {
    // Arrange / Act
    render(<PlanningIntervalCapacitySection planningIntervalKey={7} />)

    // Assert
    expect(screen.getAllByText('IP').length).toBeGreaterThan(0)
    expect(screen.getByText('25.5')).toBeInTheDocument()
    expect(screen.getByText('34.5')).toBeInTheDocument()
    expect(screen.getByText('60')).toBeInTheDocument()
    expect(screen.getAllByText('—')).toHaveLength(3)
  })

  it('opens a team’s plan over the planning interval with its sprint days off', async () => {
    // Arrange
    render(<PlanningIntervalCapacitySection planningIntervalKey={7} />)

    // Act
    await userEvent.click(
      screen.getByRole('button', { name: 'Open the plan for Team Atlas' }),
    )

    // Assert
    expect(mockPlan).toHaveBeenCalledWith({
      team: { id: 't1', name: 'Team Atlas', isActive: true },
      teamType: 'Team',
      from: '2026-01-05',
      to: '2026-03-01',
      daysOff: ['2026-01-07'],
    })
  })

  it('opens a team of teams’ plan as a team of teams, with no days off', async () => {
    // Arrange
    render(<PlanningIntervalCapacitySection planningIntervalKey={7} />)

    // Act
    await userEvent.click(
      screen.getByRole('button', { name: 'Open the plan for Group Beacon' }),
    )

    // Assert
    expect(mockPlan).toHaveBeenCalledWith(
      expect.objectContaining({ teamType: 'TeamOfTeams', daysOff: undefined }),
    )
  })

  it('offers no plan to someone who may not view teams', () => {
    // Arrange
    mockPermissions = []

    // Act
    render(<PlanningIntervalCapacitySection planningIntervalKey={7} />)

    // Assert
    expect(
      screen.queryByRole('button', { name: /Open the plan/ }),
    ).not.toBeInTheDocument()
  })
})
