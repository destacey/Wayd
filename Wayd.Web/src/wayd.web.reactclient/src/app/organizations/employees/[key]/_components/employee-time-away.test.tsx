import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import EmployeeTimeAway from './employee-time-away'

jest.unmock('dayjs')

let mockPermissions: string[] = []
jest.mock('@/src/components/contexts/auth', () => ({
  __esModule: true,
  default: () => ({
    hasPermissionClaim: (permission: string) =>
      mockPermissions.includes(permission),
  }),
}))

jest.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: jest.fn(), error: jest.fn() }),
}))

const mockReasonsArgs = jest.fn()
const mockRemove = jest.fn()
jest.mock('@/src/store/features/organization/employee-time-away-api', () => ({
  useGetTimeAwayReasonsQuery: (arg: unknown, options: unknown) => {
    mockReasonsArgs(arg, options)
    return {
      data: [
        { id: 1, code: 'PaidTimeOff', name: 'Paid time off', order: 1 },
        { id: 3, code: 'Training', name: 'Training', order: 3 },
      ],
    }
  },
  useRemoveEmployeeTimeAwayMutation: () => [mockRemove],
}))

const mockTimeAwayForm = jest.fn()
jest.mock(
  '@/src/app/organizations/_components/time-away/time-away-form',
  () => ({
    __esModule: true,
    default: (props: unknown) => {
      mockTimeAwayForm(props)
      return <div data-testid="time-away-form" />
    },
  }),
)

const mockCalendarForm = jest.fn()
jest.mock('./employee-holiday-calendar-form', () => ({
  __esModule: true,
  SYSTEM_DEFAULT_LABEL: 'System default',
  default: (props: unknown) => {
    mockCalendarForm(props)
    return <div data-testid="calendar-form" />
  },
}))

const entry = {
  id: 'away-1',
  start: '2027-02-01',
  end: '2027-02-05',
  percentAway: 50,
}

const renderSection = (timeAway: Record<string, unknown>) =>
  render(
    <EmployeeTimeAway
      employeeId="employee-1"
      timeAway={timeAway as never}
      isLoading={false}
      onRefresh={jest.fn()}
    />,
  )

describe('EmployeeTimeAway', () => {
  beforeEach(() => {
    jest.clearAllMocks()
    mockPermissions = []
    mockRemove.mockResolvedValue({})
  })

  it('shows a teammate that the person is away, with no reason and nothing to change', () => {
    renderSection({ canManage: false, entries: [entry] })

    expect(screen.getByRole('cell', { name: 'Half day' })).toBeInTheDocument()
    expect(screen.queryByText('Reason')).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Record Time Away' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Change' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Remove' }),
    ).not.toBeInTheDocument()
    // The reasons are not even looked up for someone who is not shown them.
    expect(mockReasonsArgs.mock.calls[0][1]).toEqual({ skip: true })
  })

  it('reads a part of the day in hours of the working day', () => {
    renderSection({
      canManage: false,
      workingDayHours: 6,
      entries: [{ ...entry, percentAway: 25 }],
    })

    expect(screen.getByRole('cell', { name: '1.5 h' })).toBeInTheDocument()
  })

  it('shows the reason by name to someone who may manage the time away', () => {
    renderSection({
      canManage: true,
      entries: [{ ...entry, reason: 'Training' }],
    })

    expect(screen.getByText('Reason')).toBeInTheDocument()
    expect(screen.getByRole('cell', { name: 'Training' })).toBeInTheDocument()
    expect(mockReasonsArgs.mock.calls[0][1]).toEqual({ skip: false })
  })

  it('records afresh and changes a stretch through the same form', async () => {
    const user = userEvent.setup()
    renderSection({
      canManage: true,
      entries: [{ ...entry, reason: 'PaidTimeOff' }],
    })

    await user.click(screen.getByRole('button', { name: 'Record Time Away' }))
    expect(mockTimeAwayForm).toHaveBeenLastCalledWith(
      expect.objectContaining({
        employeeId: 'employee-1',
        timeAway: undefined,
      }),
    )

    await user.click(screen.getByRole('button', { name: 'Change' }))
    expect(mockTimeAwayForm).toHaveBeenLastCalledWith(
      expect.objectContaining({
        timeAway: expect.objectContaining({ id: 'away-1' }),
      }),
    )
  })

  it('removes a stretch once confirmed', async () => {
    const user = userEvent.setup()
    renderSection({
      canManage: true,
      entries: [{ ...entry, reason: 'PaidTimeOff' }],
    })

    await user.click(screen.getByRole('button', { name: 'Remove' }))
    expect(mockRemove).not.toHaveBeenCalled()
    await user.click(
      screen.getAllByRole('button', { name: 'Remove' }).at(-1) as HTMLElement,
    )

    expect(mockRemove).toHaveBeenCalledWith({
      employeeId: 'employee-1',
      timeAwayId: 'away-1',
    })
  })

  it('names the calendar the person follows, or says they follow the system default', () => {
    const { unmount } = renderSection({ canManage: false, entries: [] })
    expect(screen.getByText('System default')).toBeInTheDocument()
    unmount()

    renderSection({
      canManage: false,
      holidayCalendar: { id: 'calendar-1', key: 1, name: 'United Kingdom' },
      entries: [],
    })
    expect(screen.getByText('United Kingdom')).toBeInTheDocument()
  })

  it('offers to change the calendar only with Employees Update and Holiday Calendars View', async () => {
    const user = userEvent.setup()
    const timeAway = {
      canManage: false,
      holidayCalendar: { id: 'calendar-1', key: 1, name: 'United Kingdom' },
      entries: [],
    }

    for (const permissions of [
      [],
      ['Permissions.Employees.Update'],
      ['Permissions.HolidayCalendars.View'],
    ]) {
      mockPermissions = permissions
      const { unmount } = renderSection(timeAway)
      expect(
        screen.queryByRole('button', { name: 'Change' }),
      ).not.toBeInTheDocument()
      unmount()
    }

    mockPermissions = [
      'Permissions.Employees.Update',
      'Permissions.HolidayCalendars.View',
    ]
    renderSection(timeAway)
    await user.click(screen.getByRole('button', { name: 'Change' }))

    expect(mockCalendarForm).toHaveBeenLastCalledWith(
      expect.objectContaining({
        employeeId: 'employee-1',
        holidayCalendar: expect.objectContaining({ id: 'calendar-1' }),
      }),
    )
  })
})
