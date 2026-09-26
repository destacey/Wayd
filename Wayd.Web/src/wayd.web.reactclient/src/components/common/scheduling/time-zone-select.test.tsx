import { render, screen } from '@testing-library/react'
import { TimeZoneDto } from '@/src/services/wayd-api'
import TimeZoneSelect, { timeZoneLabel } from './time-zone-select'

const mockUseGetTimeZonesQuery = jest.fn()

jest.mock('@/src/store/features/common/time-zones-api', () => ({
  useGetTimeZonesQuery: () => mockUseGetTimeZonesQuery(),
}))

const timeZones: TimeZoneDto[] = [
  { id: 'America/Chicago', currentOffset: '-05:00' },
  { id: 'UTC', currentOffset: '+00:00' },
]

describe('TimeZoneSelect', () => {
  beforeEach(() => {
    mockUseGetTimeZonesQuery.mockReturnValue({
      data: timeZones,
      isLoading: false,
    })
  })

  it('renders a combobox with the default placeholder', () => {
    // Act
    render(<TimeZoneSelect />)

    // Assert
    expect(screen.getByRole('combobox')).toBeInTheDocument()
    // getByPlaceholderText does not work with AntD Select
    expect(screen.getByText('Select a time zone')).toBeInTheDocument()
  })

  it('shows the selected zone with its offset', () => {
    // Act
    render(<TimeZoneSelect value="America/Chicago" />)

    // Assert
    expect(screen.getByText('(UTC-05:00) America/Chicago')).toBeInTheDocument()
  })

  it('renders while the zones are still loading', () => {
    // Arrange
    mockUseGetTimeZonesQuery.mockReturnValue({
      data: undefined,
      isLoading: true,
    })

    // Act
    render(<TimeZoneSelect />)

    // Assert
    expect(screen.getByRole('combobox')).toBeInTheDocument()
  })
})

describe('timeZoneLabel', () => {
  it('labels a zone with its current offset', () => {
    // Act
    const label = timeZoneLabel({ id: 'Asia/Kolkata', currentOffset: '+05:30' })

    // Assert
    expect(label).toBe('(UTC+05:30) Asia/Kolkata')
  })
})
