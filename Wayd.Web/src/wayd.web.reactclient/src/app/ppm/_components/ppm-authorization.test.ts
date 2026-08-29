import { canActOnPpmRecord } from './ppm-authorization'

describe('canActOnPpmRecord', () => {
  it('allows the action when the user has both the claim and leadership', () => {
    // Arrange / Act
    const result = canActOnPpmRecord(true, true)

    // Assert
    expect(result).toBe(true)
  })

  it('denies the action to a user holding the claim but not leadership', () => {
    // Arrange — the case a claim-only gate would wrongly allow: the menu
    // offers an action the aggregate then rejects.
    // Act
    const result = canActOnPpmRecord(true, false)

    // Assert
    expect(result).toBe(false)
  })

  it('denies the action to a leader without the claim', () => {
    // Arrange / Act
    const result = canActOnPpmRecord(false, true)

    // Assert
    expect(result).toBe(false)
  })

  it('denies the action while the record is still loading', () => {
    // Arrange — the flag is absent until the record arrives, and an undefined
    // flag must not read as permission.
    // Act
    const result = canActOnPpmRecord(true, undefined)

    // Assert
    expect(result).toBe(false)
  })

  it('returns a boolean rather than the undefined flag', () => {
    // Arrange / Act
    const result = canActOnPpmRecord(true, undefined)

    // Assert — menu gating reads this directly, so it must not be falsy-ish.
    expect(result).toStrictEqual(false)
  })
})
