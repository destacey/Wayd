import { asInstant } from './instant'
import { isStagedLinkOpen } from './staged-link'

const now = Date.parse('2026-11-21T12:00:00Z')

describe('isStagedLinkOpen', () => {
  it('is open before the link expires', () => {
    // Arrange
    const expiresAt = asInstant('2026-11-22T12:00:00Z')

    // Act
    const open = isStagedLinkOpen(expiresAt, now)

    // Assert
    expect(open).toBe(true)
  })

  it('is still open at the moment of expiry, as the API treats it', () => {
    // Arrange
    const expiresAt = asInstant('2026-11-21T12:00:00Z')

    // Act
    const open = isStagedLinkOpen(expiresAt, now)

    // Assert
    expect(open).toBe(true)
  })

  it('is closed once the link has expired', () => {
    // Arrange
    const expiresAt = asInstant('2026-11-21T11:59:59Z')

    // Act
    const open = isStagedLinkOpen(expiresAt, now)

    // Assert
    expect(open).toBe(false)
  })

  it('is closed when the link has no expiry', () => {
    // Arrange / Act
    const open = isStagedLinkOpen(null, now)

    // Assert
    expect(open).toBe(false)
  })
})
