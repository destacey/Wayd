import { formatTimeInStatus, secondsSince } from './time-in-status'

describe('formatTimeInStatus', () => {
  it.each([
    [0, 'under a minute'],
    [59, 'under a minute'],
    [60, '1 minute'],
    [59 * 60 + 59, '59 minutes'],
    [3600, '1 hour'],
    [23 * 3600 + 3599, '23 hours'],
    [86400, '1 day'],
    [92 * 86400 + 86399, '92 days'],
  ])('formats %i seconds as "%s"', (seconds, expected) => {
    // Act
    const result = formatTimeInStatus(seconds)

    // Assert
    expect(result).toBe(expected)
  })
})

describe('secondsSince', () => {
  it('counts whole seconds from the instant to now', () => {
    // Arrange
    const instant = new Date('2026-06-01T00:00:00Z')
    const now = instant.getTime() + 90_500

    // Act
    const result = secondsSince(instant, now)

    // Assert
    expect(result).toBe(90)
  })

  it('reads an instant that arrived as an ISO string', () => {
    // Arrange — the shape a generated Date-typed field has at runtime.
    const now = new Date('2026-06-01T00:01:30Z').getTime()

    // Act
    const result = secondsSince('2026-06-01T00:00:00Z', now)

    // Assert
    expect(result).toBe(90)
  })

  it('never goes negative when the clock is behind the instant', () => {
    // Arrange
    const instant = new Date('2026-06-01T00:00:00Z')

    // Act
    const result = secondsSince(instant, instant.getTime() - 5000)

    // Assert
    expect(result).toBe(0)
  })
})
