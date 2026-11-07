import { formatDuration } from './duration'

describe('formatDuration', () => {
  const end = '2026-11-07T12:00:00Z'

  it.each([
    ['2026-11-07T11:59:15Z', '45s'],
    ['2026-11-07T11:57:46Z', '2m 14s'],
    ['2026-11-07T11:55:00Z', '5m 00s'],
    ['2026-11-07T10:55:00Z', '1h 05m'],
    ['2026-11-07T12:00:03Z', '0s'],
  ])('should format a start of %s as %s', (start, expected) => {
    // Arrange / Act
    const result = formatDuration(start, end)

    // Assert
    expect(result).toBe(expected)
  })

  it('should accept Date instances', () => {
    // Arrange
    const start = new Date('2026-11-07T11:58:30Z')

    // Act
    const result = formatDuration(start, new Date(end))

    // Assert
    expect(result).toBe('1m 30s')
  })
})
