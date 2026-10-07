import { daysRemaining, formatInstantInZone, percentageElapsed } from './dates'

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

describe('percentageElapsed', () => {
  it('should return 0% at the start', () => {
    const start = new Date('2025-01-01T00:00:00')
    const end = new Date('2025-01-15T00:00:00')
    const now = new Date('2025-01-01T00:00:00')

    expect(percentageElapsed(start, end, now)).toEqual(0)
  })

  it('should return 50% at the midpoint', () => {
    const start = new Date('2025-01-01T00:00:00')
    const end = new Date('2025-01-15T00:00:00')
    const now = new Date('2025-01-08T00:00:00')

    expect(percentageElapsed(start, end, now)).toEqual(50)
  })

  it('should return 100% at the end', () => {
    const start = new Date('2025-01-01T00:00:00')
    const end = new Date('2025-01-15T00:00:00')
    const now = new Date('2025-01-15T00:00:00')

    expect(percentageElapsed(start, end, now)).toEqual(100)
  })

  it('should cap at 100% if past the end date', () => {
    const start = new Date('2025-01-01T00:00:00')
    const end = new Date('2025-01-15T00:00:00')
    const now = new Date('2025-01-20T00:00:00')

    expect(percentageElapsed(start, end, now)).toEqual(100)
  })

  it('should return 0% if before the start date', () => {
    const start = new Date('2025-01-10T00:00:00')
    const end = new Date('2025-01-20T00:00:00')
    const now = new Date('2025-01-05T00:00:00')

    expect(percentageElapsed(start, end, now)).toEqual(0)
  })

  it('should calculate percentage based on whole days', () => {
    // 10 day period, at start of day 5 (4 elapsed days = 40%)
    // Note: Time component is ignored, dates normalized to midnight UTC
    const start = new Date('2025-01-01T00:00:00')
    const end = new Date('2025-01-11T00:00:00')
    const now = new Date('2025-01-05T12:00:00')

    expect(percentageElapsed(start, end, now)).toEqual(40)
  })

  it('should handle same start and end date', () => {
    const start = new Date('2025-01-01T00:00:00')
    const end = new Date('2025-01-01T00:00:00')
    const now = new Date('2025-01-01T12:00:00')

    expect(percentageElapsed(start, end, now)).toEqual(0)
  })

  it('should use current time when reference date not provided', () => {
    const now = new Date()
    const start = new Date(now.getTime() - 5 * 24 * 60 * 60 * 1000) // 5 days ago
    const end = new Date(now.getTime() + 5 * 24 * 60 * 60 * 1000) // 5 days from now

    const percentage = percentageElapsed(start, end)

    // Should be close to 50% (within 1% to account for test execution time)
    expect(percentage).toBeGreaterThan(49)
    expect(percentage).toBeLessThan(51)
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

  it('calculates percentage elapsed from calendar dates', () => {
    // Act
    const result = percentageElapsed('2025-01-01', '2025-01-11', '2025-01-05')

    // Assert
    expect(result).toEqual(40)
  })
})
