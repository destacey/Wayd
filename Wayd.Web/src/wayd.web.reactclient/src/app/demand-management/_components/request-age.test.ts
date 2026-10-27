import { ageColor, daysSince, formatDays } from './request-age'

describe('daysSince', () => {
  it('counts whole days and never goes below zero', () => {
    // Arrange
    const now = new Date('2026-10-13T12:00:00Z')

    // Act
    const elapsed = daysSince(new Date('2026-10-01T13:00:00Z'), now)
    const future = daysSince(new Date('2026-10-14T00:00:00Z'), now)

    // Assert
    expect(elapsed).toBe(11)
    expect(future).toBe(0)
  })
})

describe('formatDays', () => {
  it.each([
    [0, '<1d'],
    [1, '1d'],
    [12, '12d'],
  ])('shows %i days as %s', (days, expected) => {
    // Act
    const text = formatDays(days)

    // Assert
    expect(text).toBe(expected)
  })
})

describe('ageColor', () => {
  it.each([
    [6, undefined],
    [7, 'warning'],
    [20, 'warning'],
    [21, 'error'],
  ])('colours %i days as %s', (days, expected) => {
    // Act
    const color = ageColor(days)

    // Assert
    expect(color).toBe(expected)
  })
})
