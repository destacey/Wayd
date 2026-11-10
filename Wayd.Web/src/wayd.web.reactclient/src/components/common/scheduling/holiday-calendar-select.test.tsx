import { render, screen } from '@testing-library/react'
import HolidayCalendarSelect from './holiday-calendar-select'

let canView = true
const useGetHolidayCalendarsQuery = vi.fn()

vi.mock('@/src/components/contexts/auth', () => ({
  default: () => ({ hasPermissionClaim: () => canView }),
}))

vi.mock('@/src/store/features/organization/holiday-calendars-api', () => ({
  useGetHolidayCalendarsQuery: (...args: unknown[]) =>
    useGetHolidayCalendarsQuery(...args),
}))

const current = { id: 'cal-us', key: 1, name: 'United States' }

describe('HolidayCalendarSelect', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    canView = true
  })

  it('shows the current calendar by name when the calendars cannot be listed', () => {
    // Arrange
    canView = false
    useGetHolidayCalendarsQuery.mockReturnValue({
      data: undefined,
      isLoading: false,
    })

    // Act
    render(
      <HolidayCalendarSelect
        value="cal-us"
        current={current}
        emptyLabel="System default"
        aria-label="Holiday Calendar"
      />,
    )

    // Assert
    expect(screen.getByText('United States')).toBeInTheDocument()
    expect(screen.queryByText('cal-us')).not.toBeInTheDocument()
    expect(screen.getByRole('combobox')).toBeDisabled()
    expect(useGetHolidayCalendarsQuery).toHaveBeenCalledWith(undefined, {
      skip: true,
    })
  })

  it('marks the default calendar among the listed ones', () => {
    // Arrange
    useGetHolidayCalendarsQuery.mockReturnValue({
      data: [{ ...current, holidayCount: 11, isDefault: true }],
      isLoading: false,
    })

    // Act
    render(
      <HolidayCalendarSelect
        value="cal-us"
        current={current}
        emptyLabel="System default"
      />,
    )

    // Assert
    expect(screen.getByText('United States (default)')).toBeInTheDocument()
  })
})
