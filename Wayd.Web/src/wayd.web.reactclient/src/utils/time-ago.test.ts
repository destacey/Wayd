import { formatTimeAgo } from './time-ago'

const now = new Date('2026-10-24T12:00:00Z')
const ago = (seconds: number) => new Date(now.getTime() - seconds * 1000)

describe('formatTimeAgo', () => {
  it.each([
    [10, 'just now'],
    [59, 'just now'],
    [60, '1 minute ago'],
    [2 * 60 * 60, '2 hours ago'],
    [24 * 60 * 60, 'yesterday'],
    [3 * 24 * 60 * 60, '3 days ago'],
    [7 * 24 * 60 * 60, 'last week'],
    [21 * 24 * 60 * 60, '3 weeks ago'],
    [400 * 24 * 60 * 60, 'last year'],
  ])('describes %i seconds ago as "%s"', (seconds, expected) => {
    // Act & Assert
    expect(formatTimeAgo(ago(seconds), now)).toBe(expected)
  })

  it('accepts the ISO string an API response actually carries', () => {
    // Act & Assert
    expect(formatTimeAgo('2026-10-24T10:00:00Z', now)).toBe('2 hours ago')
  })
})
