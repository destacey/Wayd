import { act, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { TeamCapacityPlanDto } from '@/src/services/wayd-api'
import TeamCapacityPlan from './team-capacity-plan'

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

const mockSuccess = jest.fn()
jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: mockSuccess, error: jest.fn() }),
}))

let mockPlan: TeamCapacityPlanDto | undefined
const mockPlanArgs = jest.fn()
jest.mock('@/src/store/features/organization/team-capacity-api', () => ({
  useGetTeamCapacityPlanQuery: (arg: unknown) => {
    mockPlanArgs(arg)
    return { currentData: mockPlan, isFetching: false, error: undefined }
  },
}))

const mockChangeAllocations = jest.fn()
jest.mock('@/src/store/features/organization/employee-allocations-api', () => ({
  useChangeEmployeeAllocationsMutation: () => [mockChangeAllocations],
}))

let mockMembers: unknown[] | undefined
jest.mock('@/src/store/features/organization/team-members-api', () => ({
  useGetTeamMembersQuery: () => ({ data: mockMembers }),
  useGetTeamOfTeamsMembersQuery: () => ({ data: undefined }),
}))

const mockTimeAwayForm = jest.fn()
jest.mock('../time-away/time-away-form', () => ({
  __esModule: true,
  default: (props: unknown) => {
    mockTimeAwayForm(props)
    return <div data-testid="time-away-form" />
  },
}))

const mockChangeForm = jest.fn()
jest.mock('../allocations/change-allocation-form', () => ({
  __esModule: true,
  default: (props: unknown) => {
    mockChangeForm(props)
    return <div data-testid="change-form" />
  },
}))

// A two-week sprint, Monday to the Friday after.
const FROM = '2027-02-01'
const TO = '2027-02-12'
const DAYS = [
  '2027-02-01',
  '2027-02-02',
  '2027-02-03',
  '2027-02-04',
  '2027-02-05',
  '2027-02-08',
  '2027-02-09',
  '2027-02-10',
  '2027-02-11',
  '2027-02-12',
]

function plan(canManage = true): TeamCapacityPlanDto {
  return {
    capacity: {
      from: FROM,
      to: TO,
      workingDays: 10,
      allocatedPersonDays: 5,
      deliveryPersonDays: 5,
      awayPersonDays: 0,
      canManage,
      workingDayHours: 8,
      roles: [],
      people: [
        {
          employee: { id: 'ada', key: 1, name: 'Ada Lovelace' },
          allocatedPersonDays: 5,
          deliveryPersonDays: 5,
          elsewherePersonDays: 0,
          awayPersonDays: 0,
          timeAway: [],
          allocations: [],
          elsewhereAllocations: [],
        },
      ],
    },
    days: DAYS,
    people: [
      {
        employeeId: 'ada',
        canManageTimeAway: true,
        days: [
          {
            date: '2027-02-03',
            isHoliday: true,
            percentAway: 0,
            elsewherePercent: 0,
          },
        ],
        rows: [
          {
            countsTowardDeliveryCapacity: true,
            days: DAYS.map((date) => ({
              date,
              percent: 50,
              personDays: date === '2027-02-03' ? 0 : 0.5,
            })),
          },
        ],
      },
    ],
  }
}

const renderPlan = () =>
  render(
    <TeamCapacityPlan
      team={{ id: 'team-1', name: 'Cards', isActive: true }}
      teamType="Team"
      from={FROM}
      to={TO}
    />,
  )

describe('TeamCapacityPlan', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockPermissions = ['Permissions.Teams.Update']
    mockPlan = plan()
    mockMembers = [{ employee: { id: 'member-1' } }]
    mockChangeAllocations.mockResolvedValue({ data: undefined })
  })

  it('reads the team over the plan’s dates and shows a person against its weeks', () => {
    renderPlan()

    expect(mockPlanArgs).toHaveBeenCalledWith({
      teamId: 'team-1',
      teamType: 'Team',
      from: FROM,
      to: TO,
    })
    expect(screen.getByRole('link', { name: 'Ada Lovelace' })).toHaveAttribute(
      'href',
      '/organizations/employees/1',
    )
    // The table repeats its header to measure columns, so each title is there twice.
    expect(screen.getAllByText('Week 1').length).toBeGreaterThan(0)
    expect(screen.getAllByText('Week 2').length).toBeGreaterThan(0)
    expect(screen.queryAllByText('Week 3')).toHaveLength(0)
    expect(screen.getByTestId('cell-ada|-w:0')).toHaveTextContent('50%')
  })

  it('expands a week into its working days', async () => {
    renderPlan()

    await userEvent.click(
      screen.getByRole('button', { name: /Expand week of Feb 1/ }),
    )

    expect(screen.getAllByText('Wed 3').length).toBeGreaterThan(0)
    expect(screen.getByTestId('cell-ada|-d:2027-02-03')).toHaveTextContent(
      '50%',
    )
    expect(screen.queryByTestId('cell-ada|-w:0')).not.toBeInTheDocument()
  })

  it('saves 100% for the first week and 0% for the second as changes over those weeks only', async () => {
    renderPlan()

    await userEvent.click(screen.getByRole('button', { name: 'Edit Plan' }))
    const firstWeek = screen.getByRole('spinbutton', {
      name: /Week of Feb 1 – Feb 7/,
    })
    const secondWeek = screen.getByRole('spinbutton', {
      name: /Week of Feb 8 – Feb 12/,
    })
    await userEvent.clear(firstWeek)
    await userEvent.type(firstWeek, '100')
    await userEvent.clear(secondWeek)
    await userEvent.type(secondWeek, '0')
    // The save's updates land after its awaits, which only act flushes here.
    await act(async () => {
      await userEvent.click(screen.getByRole('button', { name: 'Save' }))
    })

    expect(mockSuccess).toHaveBeenCalledWith('Plan saved.')
    expect(mockChangeAllocations).toHaveBeenCalledTimes(1)
    expect(mockChangeAllocations).toHaveBeenCalledWith({
      employeeId: 'ada',
      request: {
        changes: [
          {
            teamId: 'team-1',
            roleId: undefined,
            start: '2027-02-01',
            end: '2027-02-07',
            percent: 100,
          },
          {
            teamId: 'team-1',
            roleId: undefined,
            start: '2027-02-08',
            end: '2027-02-12',
            percent: 0,
          },
        ],
      },
    })
    expect(
      screen.getByRole('button', { name: 'Edit Plan' }),
    ).toBeInTheDocument()
  })

  it('keeps the edits of a person whose changes were refused, with why', async () => {
    mockChangeAllocations.mockResolvedValue({
      error: { detail: 'The employee would be 120% allocated on Feb 1, 2027.' },
    })
    renderPlan()

    await userEvent.click(screen.getByRole('button', { name: 'Edit Plan' }))
    const firstWeek = screen.getByRole('spinbutton', {
      name: /Week of Feb 1 – Feb 7/,
    })
    await userEvent.clear(firstWeek)
    await userEvent.type(firstWeek, '100')
    // The save's updates land after its awaits, which only act flushes here.
    await act(async () => {
      await userEvent.click(screen.getByRole('button', { name: 'Save' }))
    })

    expect(
      await screen.findByText(
        'Ada Lovelace: The employee would be 120% allocated on Feb 1, 2027.',
      ),
    ).toBeInTheDocument()
    expect(firstWeek).toHaveValue('100')
    expect(mockSuccess).not.toHaveBeenCalled()
  })

  it('ends a new allocation with the plan for a non-member and leaves a member open-ended', async () => {
    renderPlan()

    await userEvent.click(
      screen.getByRole('button', { name: 'Allocate Person' }),
    )

    const props = mockChangeForm.mock.calls[0][0]
    expect(props.initialDates).toEqual({ start: FROM, end: TO })
    expect(props.endDateFor('someone-else')).toBe(TO)
    expect(props.endDateFor('member-1')).toBeUndefined()
  })

  it.each([
    ['without the update permission', [], true],
    [
      'to a caller who cannot manage the team',
      ['Permissions.Teams.Update'],
      false,
    ],
  ])('is read-only %s', (_, permissions, canManage) => {
    mockPermissions = permissions as string[]
    mockPlan = plan(canManage as boolean)

    renderPlan()

    expect(
      screen.queryByRole('button', { name: 'Edit Plan' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Allocate Person' }),
    ).not.toBeInTheDocument()
  })

  it('records time away for a person from their row, starting on the plan’s first day', async () => {
    renderPlan()

    await userEvent.click(
      screen.getAllByRole('button', {
        name: 'Record time away for Ada Lovelace',
      })[0],
    )

    expect(mockTimeAwayForm).toHaveBeenCalledWith(
      expect.objectContaining({
        employeeId: 'ada',
        workingDayHours: 8,
        initialDates: { start: FROM, end: FROM },
      }),
    )
  })

  it('shows the plan’s totals', () => {
    renderPlan()

    const value = (title: string) =>
      within(
        screen.getByText(title).closest('.ant-statistic') as HTMLElement,
      ).getByText(/\d/).textContent
    expect(value('Delivery person-days')).toBe('5')
    expect(value('Working days')).toBe('10')
  })
})
