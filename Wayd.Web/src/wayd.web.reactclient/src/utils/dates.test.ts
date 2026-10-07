import {
  dayOfPeriod,
  daysRemaining,
  formatInstantInZone,
  percentageElapsed,
  pickerTimeZoneNote,
} from './dates'

describe('pickerTimeZoneNote', () => {
  const at = new Date('2026-09-28T17:00:00Z')

  it('names the viewer’s zone and the team’s when they differ', () => {
    // Arrange / Act / Assert
    expect(
      pickerTimeZoneNote('America/Chicago', 'America/Los_Angeles', at),
    ).toBe(
      "your time zone, PDT (America/Los_Angeles); the team's is America/Chicago",
    )
  })

  it('names only the viewer’s zone when the team shares it', () => {
    // Arrange / Act / Assert
    expect(pickerTimeZoneNote('UTC', 'UTC', at)).toBe(
      'your time zone, UTC (UTC)',
    )
  })
})

describe('formatInstantInZone', () => {
  it('shows the clock in the given zone, with its abbreviation', () => {
    expect(formatInstantInZone('2026-09-15T05:00:00Z', 'America/Chicago')).toBe(
      'Sep 15, 2026, 12:00 AM CDT',
    )
  })

  it('takes a Date as well as an ISO string', () => {
    expect(formatInstantInZone(new Date('2026-09-15T05:00:00Z'), 'UTC')).toBe(
      'Sep 15, 2026, 5:00 AM UTC',
    )
  })
})

describe('daysRemaining', () => {
  it('should return the number of days remaining for a future date', () => {
    const futureDate = new Date()
    futureDate.setDate(futureDate.getDate() + 5) // 5 days from now
    expect(daysRemaining(futureDate)).toEqual(5)
  })

  it('should return a negative number for a past date', () => {
    const pastDate = new Date()
    pastDate.setDate(pastDate.getDate() - 3) // 3 days ago
    expect(daysRemaining(pastDate)).toEqual(-3)
  })

  it('should return 0 for today', () => {
    const today = new Date()
    expect(daysRemaining(today)).toEqual(0)
  })

  it('should return 1 for tomorrow', () => {
    const tomorrow = new Date()
    tomorrow.setDate(tomorrow.getDate() + 1) // tomorrow
    // the +0 is to make sure the result is not -0
    expect(daysRemaining(tomorrow) + 0).toEqual(1)
  })

  it('should handle end of month correctly', () => {
    // Use fixed dates to avoid timing issues
    const today = new Date('2024-01-15T10:30:00Z')
    const endOfMonth = new Date('2024-01-31T15:45:00Z')

    // Both dates should be normalized to midnight UTC by daysRemaining
    // From Jan 15 to Jan 31 = 16 days
    expect(daysRemaining(endOfMonth, today)).toEqual(16)
  })

  it('should handle leap years correctly', () => {
    const leapYearDate = new Date('2024-02-29T00:00:00Z')
    const today = new Date('2024-02-28T00:00:00Z')
    expect(daysRemaining(leapYearDate, today)).toEqual(1)
  })
})

describe('dayOfPeriod', () => {
  it('counts the first and last days, as the sprint timeline does', () => {
    // Arrange / Act
    const result = dayOfPeriod('2026-09-28', '2026-10-11', '2026-10-06')

    // Assert
    expect(result).toEqual({ currentDay: 9, totalDays: 14 })
  })

  it('is day 0 before the period starts', () => {
    // Arrange / Act
    const result = dayOfPeriod('2026-09-28', '2026-10-11', '2026-09-20')

    // Assert
    expect(result.currentDay).toBe(0)
  })

  it('stays on the last day after the period ends', () => {
    // Arrange / Act
    const result = dayOfPeriod('2026-09-28', '2026-10-11', '2026-10-20')

    // Assert
    expect(result.currentDay).toBe(14)
  })
})

describe('percentageElapsed', () => {
  it('is the share of the period’s days reached, counting today', () => {
    // Arrange / Act — day 9 of 14
    const result = percentageElapsed('2026-09-28', '2026-10-11', '2026-10-06')

    // Assert
    expect(Math.round(result)).toBe(64)
  })

  it('is 0% before the period starts', () => {
    // Arrange / Act / Assert
    expect(percentageElapsed('2026-09-28', '2026-10-11', '2026-09-27')).toBe(0)
  })

  it('is 100% on the last day and after it', () => {
    // Arrange / Act / Assert
    expect(percentageElapsed('2026-09-28', '2026-10-11', '2026-10-11')).toBe(
      100,
    )
    expect(percentageElapsed('2026-09-28', '2026-10-11', '2026-10-20')).toBe(
      100,
    )
  })

  it('is 100% on a one-day period', () => {
    // Arrange / Act / Assert
    expect(percentageElapsed('2026-09-28', '2026-09-28', '2026-09-28')).toBe(
      100,
    )
  })
})

describe('calendar date inputs', () => {
  it('counts days remaining between calendar dates', () => {
    // Act
    const result = daysRemaining('2026-10-09', '2026-09-28')

    // Assert
    expect(result).toEqual(11)
  })

  it('counts a calendar date against a local reference date by its day', () => {
    // Arrange
    const lateOnTheDay = new Date(2026, 8, 28, 23, 59)

    // Act
    const result = daysRemaining('2026-09-29', lateOnTheDay)

    // Assert
    expect(result).toEqual(1)
  })
})
