import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { asInstant } from '@/src/utils/instant'
import StagedLinkTag from './staged-link-tag'

const now = Date.parse('2026-11-21T12:00:00Z')
const renewal = 'Use Set Sign-in Tenant to give them another 14 days.'

describe('StagedLinkTag', () => {
  it('reads as pending before the link expires, and says when it expires', async () => {
    // Arrange
    render(
      <StagedLinkTag
        label="Tenant migration"
        expiresAt={asInstant('2026-11-30T12:00:00Z')}
        now={now}
        renewal={renewal}
      />,
    )

    // Act
    await userEvent.hover(screen.getByText('Tenant migration pending'))

    // Assert
    expect(await screen.findByText(/^Expires /)).toBeInTheDocument()
  })

  it('reads as expired once the link has expired, and says how to renew it', async () => {
    // Arrange
    render(
      <StagedLinkTag
        label="First sign-in"
        expiresAt={asInstant('2026-11-20T12:00:00Z')}
        now={now}
        renewal={renewal}
      />,
    )

    // Act
    await userEvent.hover(screen.getByText('First sign-in expired'))

    // Assert
    expect(
      await screen.findByText(
        /Use Set Sign-in Tenant to give them another 14 days\./,
      ),
    ).toBeInTheDocument()
  })

  it('reads as expired when the link has no expiry', () => {
    // Arrange / Act
    render(
      <StagedLinkTag
        label="Provider migration"
        expiresAt={null}
        now={now}
        renewal={renewal}
      />,
    )

    // Assert
    expect(screen.getByText('Provider migration expired')).toBeInTheDocument()
  })
})
