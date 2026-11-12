import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { IncidentDto } from '@/src/services/wayd-api'
import IncidentTeamsChat from './incident-teams-chat'

const linkChat = vi.hoisted(() =>
  vi.fn(() => ({ unwrap: () => Promise.resolve() })),
)

vi.mock(import('@/src/components/contexts/messaging'), () => ({
  useMessage: () => ({ error: vi.fn(), success: vi.fn() }),
}))

vi.mock(
  import('@/src/store/features/product-management/incidents-api'),
  () => ({
    useLinkIncidentTeamsChatMutation: () => [linkChat, { isLoading: false }],
  }),
)

const incident = (teamsChatLink: string | null, teamsChatDisplayName: string | null = null) =>
  ({
    id: '01a1d091-2de1-75d3-97d9-34eae573d1fc',
    key: 12,
    title: 'Checkout latency',
    teamsConnectorConfigured: true,
    teamsChatLink,
    teamsChatDisplayName,
    submittedBy: null,
    reportedBy: null,
    commander: null,
  }) as unknown as IncidentDto

describe('IncidentTeamsChat', () => {
  beforeEach(() => {
    linkChat.mockClear()
  })

  it('opens the stored chat without giving the new tab access back', () => {
    // Arrange
    render(
      <IncidentTeamsChat
        incident={incident(
          'https://teams.microsoft.com/l/chat/1',
          'Bridge',
        )}
        canUpdate={false}
        employees={[]}
      />,
    )

    // Act
    const chat = screen.getByRole('link', { name: 'Bridge' })

    // Assert
    expect(chat).toHaveAttribute(
      'href',
      'https://teams.microsoft.com/l/chat/1',
    )
    expect(chat).toHaveAttribute('target', '_blank')
    expect(chat).toHaveAttribute('rel', 'noopener noreferrer')
  })

  it('accepts a Teams link whose scheme is uppercase', async () => {
    // Arrange
    const user = userEvent.setup()
    render(
      <IncidentTeamsChat
        incident={incident(null)}
        canUpdate
        employees={[]}
      />,
    )

    // Act
    await user.click(
      screen.getByRole('button', { name: 'Add Teams chat for this incident' }),
    )
    await user.click(screen.getByRole('tab', { name: 'Link existing' }))
    await user.type(
      screen.getByRole('textbox', { name: 'Teams link' }),
      'HTTPS://teams.microsoft.com/l/chat/1',
    )
    await user.click(screen.getByRole('button', { name: 'Link chat' }))

    // Assert
    expect(linkChat).toHaveBeenCalledWith(
      expect.objectContaining({
        link: 'HTTPS://teams.microsoft.com/l/chat/1',
      }),
    )
    expect(
      screen.queryByText('The link must start with https://'),
    ).not.toBeInTheDocument()
  })

  it('rejects a link that is not https', async () => {
    // Arrange
    const user = userEvent.setup()
    render(
      <IncidentTeamsChat
        incident={incident(null)}
        canUpdate
        employees={[]}
      />,
    )

    // Act
    await user.click(
      screen.getByRole('button', { name: 'Add Teams chat for this incident' }),
    )
    await user.click(screen.getByRole('tab', { name: 'Link existing' }))
    await user.type(
      screen.getByRole('textbox', { name: 'Teams link' }),
      'http://teams.microsoft.com/l/chat/1',
    )
    await user.click(screen.getByRole('button', { name: 'Link chat' }))

    // Assert
    expect(
      await screen.findByText('The link must start with https://'),
    ).toBeInTheDocument()
    expect(linkChat).not.toHaveBeenCalled()
  })
})
