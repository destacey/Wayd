import { formatIncidentDuration } from './incident-format'

describe('formatIncidentDuration', () => {
  it('keeps a real zero visible as seconds', () => {
    // Arrange
    const milliseconds = 0

    // Act
    const formatted = formatIncidentDuration(milliseconds)

    // Assert
    expect(formatted).toBe('0s')
  })

  it('uses seconds under a minute so a short stretch is not zero minutes', () => {
    // Arrange
    const milliseconds = 45_000

    // Act
    const formatted = formatIncidentDuration(milliseconds)

    // Assert
    expect(formatted).toBe('45s')
  })

  it('uses hours and leftover minutes, then days past two days', () => {
    // Arrange
    const twoHours = 2 * 60 * 60 * 1000 + 15 * 60 * 1000
    const twoDays = 50 * 60 * 60 * 1000

    // Act
    const hours = formatIncidentDuration(twoHours)
    const days = formatIncidentDuration(twoDays)

    // Assert
    expect(hours).toBe('2h 15m')
    expect(days).toBe('2d 2h')
  })
})
