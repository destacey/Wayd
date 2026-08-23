import { personInitials } from './record-initials'

describe('personInitials', () => {
  it('uses the structured name fields when both are present', () => {
    // Arrange / Act
    const result = personInitials('Priya', 'Raghunathan', 'Priya Raghunathan')

    // Assert
    expect(result).toBe('PR')
  })

  it('uses whichever structured field is present', () => {
    // Arrange / Act
    const first = personInitials('Priya', undefined, undefined)
    const last = personInitials(undefined, 'Raghunathan', undefined)

    // Assert
    expect(first).toBe('P')
    expect(last).toBe('R')
  })

  it('falls back to the first and last word of a display name', () => {
    // Arrange / Act
    const result = personInitials(undefined, undefined, 'Amara Naledi Sithole')

    // Assert
    expect(result).toBe('AS')
  })

  it('returns a single letter for a one-word display name', () => {
    // Arrange / Act
    const result = personInitials(undefined, undefined, 'Prince')

    // Assert
    expect(result).toBe('P')
  })

  it('returns an empty string when nothing is available', () => {
    // Arrange / Act
    const result = personInitials(undefined, undefined, undefined)

    // Assert
    expect(result).toBe('')
  })

  it('ignores surrounding and repeated whitespace', () => {
    // Arrange / Act
    const result = personInitials(undefined, undefined, '  Wei   Chen  ')

    // Assert
    expect(result).toBe('WC')
  })
})
