import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import EmployeeTimeAway from './employee-time-away'

let mockPermissions: string[] = []
vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({
    hasPermissionClaim: (permission: string) =>
      mockPermissions.includes(permission),
  }),
}))

vi.mock('@/src/components/contexts/messaging', () => ({
  useMessage: () => ({ success: vi.fn(), error: vi.fn() }),
}))

const mockReasonsArgs = vi.fn()
const mockRemove = vi.fn()
vi.mock('@/src/store/features/organization/employee-time-away-api', () => ({
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

const mockTimeAwayForm = vi.fn()
vi.mock('@/src/app/organizations/_components/time-away/time-away-form', () => ({
  default: (props: unknown) => {
    mockTimeAwayForm(props)
    return <div data-testid="time-away-form" />
  },
}))

const mockCalendarForm = vi.fn()
vi.mock('./employee-holiday-calendar-form', () => ({
  SYSTEM_DEFAULT_LABEL: 'System default',
  default: (props: unknown) => {
    mockCalendarForm(props)
    return <div data-testid="calendar-form" />
  },
}))

const mockWorkingDayForm = vi.fn()
vi.mock(import('./employee-working-day-form'), async (importOriginal) => ({
  workingDayLabel: (await importOriginal()).workingDayLabel,
  default: (props: unknown) => {
    mockWorkingDayForm(props)
    return <div data-testid="working-day-form" />
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
      onRefresh={vi.fn()}
    />,
  )

describe('EmployeeTimeAway', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    mockPermissions = []
    mockRemove.mockResolvedValue({})
  })

  it('shows a teammate that the person is away, with no reason and nothing to change', () => {
    renderSection({ canManage: false, entries: [entry] })

    expect(
      screen.getByRole('gridcell', { name: 'Half day' }),
    ).toBeInTheDocument()
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

    expect(screen.getByRole('gridcell', { name: '1.5 h' })).toBeInTheDocument()
  })

  it('shows the reason by name to someone who may manage the time away', () => {
    renderSection({
      canManage: true,
      entries: [{ ...entry, reason: 'Training' }],
    })

    expect(screen.getByText('Reason')).toBeInTheDocument()
    expect(
      screen.getByRole('gridcell', { name: 'Training' }),
    ).toBeInTheDocument()
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
        screen.queryByRole('button', { name: 'Change holiday calendar' }),
      ).not.toBeInTheDocument()
      unmount()
    }

    mockPermissions = [
      'Permissions.Employees.Update',
      'Permissions.HolidayCalendars.View',
    ]
    renderSection(timeAway)
    await user.click(
      screen.getByRole('button', { name: 'Change holiday calendar' }),
    )

    expect(mockCalendarForm).toHaveBeenLastCalledWith(
      expect.objectContaining({
        employeeId: 'employee-1',
        holidayCalendar: expect.objectContaining({ id: 'calendar-1' }),
      }),
    )
  })

  it("says the person's own working day, or the system default's", () => {
    const { unmount } = renderSection({
      canManage: false,
      systemWorkingDayHours: 7.5,
      entries: [],
    })
    expect(screen.getByText('System default (7.5 h)')).toBeInTheDocument()
    unmount()

    renderSection({
      canManage: false,
      ownWorkingDayHours: 4,
      systemWorkingDayHours: 7.5,
      entries: [],
    })
    expect(screen.getByText('4 h')).toBeInTheDocument()
  })

  it('reads time away in hours of the working day the person has', () => {
    renderSection({
      canManage: false,
      workingDayHours: 4,
      ownWorkingDayHours: 4,
      systemWorkingDayHours: 8,
      entries: [{ ...entry, percentAway: 25 }],
    })

    expect(screen.getByRole('gridcell', { name: '1 h' })).toBeInTheDocument()
  })

  it('offers to change the working day only with Employees Update', async () => {
    const user = userEvent.setup()
    const timeAway = {
      canManage: false,
      ownWorkingDayHours: 4,
      systemWorkingDayHours: 8,
      entries: [],
    }

    const { unmount } = renderSection(timeAway)
    expect(
      screen.queryByRole('button', { name: 'Change working day' }),
    ).not.toBeInTheDocument()
    unmount()

    mockPermissions = ['Permissions.Employees.Update']
    renderSection(timeAway)
    await user.click(screen.getByRole('button', { name: 'Change working day' }))

    expect(mockWorkingDayForm).toHaveBeenLastCalledWith(
      expect.objectContaining({
        employeeId: 'employee-1',
        ownWorkingDayHours: 4,
        systemWorkingDayHours: 8,
      }),
    )
  })
})
