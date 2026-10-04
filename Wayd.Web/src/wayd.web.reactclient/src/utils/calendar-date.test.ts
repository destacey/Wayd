// parseCalendarDate's local reading needs real dayjs; the global mock only stubs format.
jest.unmock('dayjs')

import {
  calendarDateInZone,
  calendarDaysBetween,
  compareCalendarDates,
  formatCalendarDate,
  parseCalendarDate,
  toCalendarDate,
  todayCalendarDate,
} from './calendar-date'

describe('parseCalendarDate', () => {
  it('reads the date as local midnight of that day', () => {
    // Act
    const result = parseCalendarDate('2026-09-28')

    // Assert
    expect(result.year()).toBe(2026)
    expect(result.month()).toBe(8)
    expect(result.date()).toBe(28)
    expect(result.hour()).toBe(0)
  })
})

describe('toCalendarDate', () => {
  it('formats a local date as its ISO calendar day', () => {
    // Arrange
    const value = new Date(2026, 8, 28, 23, 30)

    // Act
    const result = toCalendarDate(value)

    // Assert
    expect(result).toBe('2026-09-28')
  })

  it('round-trips a parsed calendar date', () => {
    // Act
    const result = toCalendarDate(parseCalendarDate('2024-02-29'))

    // Assert
    expect(result).toBe('2024-02-29')
  })
})

describe('formatCalendarDate', () => {
  it('formats the day as written', () => {
    // Act
    const result = formatCalendarDate('2026-09-28')

    // Assert
    expect(result).toBe('Sep 28, 2026')
  })

  it('returns an empty string for a missing value', () => {
    // Act
    const result = formatCalendarDate(undefined)

    // Assert
    expect(result).toBe('')
  })
})

describe('todayCalendarDate', () => {
  it('is the local day', () => {
    // Arrange
    const now = new Date()

    // Act
    const result = todayCalendarDate()

    // Assert
    expect(result).toBe(toCalendarDate(now))
  })
})

describe('compareCalendarDates', () => {
  it('orders dates chronologically', () => {
    // Arrange
    const dates = ['2026-10-01', '2025-12-31', '2026-09-28']

    // Act
    const result = [...dates].sort(compareCalendarDates)

    // Assert
    expect(result).toEqual(['2025-12-31', '2026-09-28', '2026-10-01'])
  })

  it('sorts a missing value first', () => {
    // Act
    const result = compareCalendarDates(undefined, '2026-09-28')

    // Assert
    expect(result).toBeLessThan(0)
  })
})

describe('calendarDaysBetween', () => {
  it('counts days between calendar dates', () => {
    // Act
    const result = calendarDaysBetween('2026-09-28', '2026-10-09')

    // Assert
    expect(result).toBe(11)
  })

  it('crosses a leap day', () => {
    // Act
    const result = calendarDaysBetween('2024-02-28', '2024-03-01')

    // Assert
    expect(result).toBe(2)
  })

  it('counts a local date by the day it falls on, whatever its time', () => {
    // Arrange
    const lateOnTheStart = new Date(2026, 8, 28, 23, 59)

    // Act
    const result = calendarDaysBetween(lateOnTheStart, '2026-09-29')

    // Assert
    expect(result).toBe(1)
  })

  it('counts an ISO instant string by the viewer’s day, not its UTC date', () => {
    // Arrange
    const lateOnTheStart = new Date(2026, 8, 28, 23, 30).toISOString()

    // Act
    const result = calendarDaysBetween(lateOnTheStart, '2026-09-29')

    // Assert
    expect(result).toBe(1)
  })

  it('is negative when the end is earlier', () => {
    // Act
    const result = calendarDaysBetween('2026-09-28', '2026-09-25')

    // Assert
    expect(result).toBe(-3)
  })
})

describe('calendarDateInZone', () => {
  it('takes the day the instant falls on in the given zone', () => {
    // Arrange — 03:00 UTC on Sep 28 is still Sep 27 in Chicago
    const instant = new Date('2026-09-28T03:00:00Z')

    // Act
    const chicago = calendarDateInZone(instant, 'America/Chicago')
    const utc = calendarDateInZone(instant, 'UTC')

    // Assert
    expect(chicago).toBe('2026-09-27')
    expect(utc).toBe('2026-09-28')
  })

  it('accepts the ISO string a Date-typed field holds', () => {
    // Act
    const result = calendarDateInZone('2026-09-28T05:00:00Z', 'America/Chicago')

    // Assert
    expect(result).toBe('2026-09-28')
  })
})
