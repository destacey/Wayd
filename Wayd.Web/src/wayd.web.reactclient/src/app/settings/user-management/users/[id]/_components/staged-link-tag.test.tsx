import { render, screen } from '@testing-library/react'
import { asInstant } from '@/src/utils/instant'
import StagedLinkTag, { isStagedLinkOpen } from './staged-link-tag'

const now = Date.parse('2026-11-21T12:00:00Z')

describe('StagedLinkTag', () => {
  it('reads as pending before the link expires', () => {
    // Arrange / Act
    render(
      <StagedLinkTag
        label="Tenant migration"
        expiresAt={asInstant('2026-11-30T12:00:00Z')}
        now={now}
      />,
    )

    // Assert
    expect(screen.getByText('Tenant migration pending')).toBeInTheDocument()
  })

  it('reads as expired once the link has expired', () => {
    // Arrange / Act
    render(
      <StagedLinkTag
        label="Provider migration"
        expiresAt={asInstant('2026-11-20T12:00:00Z')}
        now={now}
      />,
    )

    // Assert
    expect(screen.getByText('Provider migration expired')).toBeInTheDocument()
  })

  it('reads as expired when the link has no expiry', () => {
    // Arrange / Act
    render(
      <StagedLinkTag label="Provider migration" expiresAt={null} now={now} />,
    )

    // Assert
    expect(screen.getByText('Provider migration expired')).toBeInTheDocument()
  })
})

describe('isStagedLinkOpen', () => {
  it('is still open at the moment of expiry, as the API treats it', () => {
    // Arrange
    const expiresAt = asInstant('2026-11-21T12:00:00Z')

    // Act
    const open = isStagedLinkOpen(expiresAt, now)

    // Assert
    expect(open).toBe(true)
  })
})
