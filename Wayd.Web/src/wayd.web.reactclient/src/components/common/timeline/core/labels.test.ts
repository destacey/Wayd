import { truncateOneDayLabel } from './labels'

describe('truncateOneDayLabel', () => {
  it('keeps labels at or below the limit unchanged', () => {
    // Arrange / Act / Assert
    expect(truncateOneDayLabel('Exactly twenty chars')).toBe(
      'Exactly twenty chars',
    )
  })

  it('truncates long labels near 20 characters at a word boundary', () => {
    // Arrange / Act
    const label = truncateOneDayLabel('This one-day item name is too long')

    // Assert
    expect(label).toBe('This one-day item…')
    expect(label.length).toBeLessThanOrEqual(20)
  })
})
