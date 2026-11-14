import { createHash } from 'node:crypto'
import { readFileSync } from 'node:fs'
import { dirname, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import type { ReactElement } from 'react'
import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MessageProvider } from '@/src/components/contexts/messaging'
import { IncidentDto } from '@/src/services/wayd-api'
import IncidentTeamsChat from './incident-teams-chat'
import styles from './incident-teams-chat.module.css'

const copyLinkSentence =
  'Open the group chat. The three dots are at the top right. Copy link is in that menu, under Mute.'

// This hash is the annotated Teams menu screenshot, so a different image at the path fails.
const teamsMenuShotHash =
  '901681c9d3540e91fec721503ae1d14ab0fec07be067dac9abd72c350b4b7ab5'

const teamsMenuShot = readFileSync(
  resolve(
    dirname(fileURLToPath(import.meta.url)),
    '../../../../../public/images/teams-copy-link-annotated.png',
  ),
)

const teamsLogo = readFileSync(
  resolve(
    dirname(fileURLToPath(import.meta.url)),
    '../../../../../public/icons/teams-logo.svg',
  ),
  'utf8',
)

const linkChat = vi.hoisted(() =>
  vi.fn(() => ({ unwrap: () => Promise.resolve() })),
)

const createChat = vi.hoisted(() =>
  vi.fn(() => ({ unwrap: () => Promise.resolve() })),
)

vi.mock(
  import('@/src/store/features/product-management/incidents-api'),
  async (importOriginal) => {
    const actual = await importOriginal()
    return {
      ...actual,
      // unwrap is the only part of the RTK trigger this component awaits.
      useCreateIncidentTeamsChatMutation: () => [
        createChat,
        { isLoading: false },
      ],
      useLinkIncidentTeamsChatMutation: () => [linkChat, { isLoading: false }],
    } as unknown as typeof actual
  },
)

const renderChat = (ui: ReactElement) =>
  render(<MessageProvider>{ui}</MessageProvider>)

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
  it('hides the add control unless a Teams connector is ready', () => {
    // Arrange
    const stored = incident(null)

    // Act
    renderChat(
      <IncidentTeamsChat
        incident={{ ...stored, teamsConnectorConfigured: false }}
        canUpdate
        employees={[]}
      />,
    )

    // Assert
    expect(
      screen.queryByRole('button', { name: 'Add Teams chat for this incident' }),
    ).not.toBeInTheDocument()
  })

  beforeEach(() => {
    linkChat.mockClear()
    createChat.mockClear()
  })

  it('gives the extra-people picker the modal body width', async () => {
    // Arrange
    const user = userEvent.setup()
    renderChat(
      <IncidentTeamsChat
        incident={incident(null)}
        canUpdate
        employees={[{ value: 'e1', label: 'Casey Submitter' }]}
      />,
    )

    // Act
    await user.click(
      screen.getByRole('button', { name: 'Add Teams chat for this incident' }),
    )

    // Assert
    const picker = screen.getByRole('combobox').closest('.ant-select')
    expect(picker).toHaveStyle({ width: '100%' })
    expect(picker?.parentElement).toHaveStyle({
      display: 'block',
      width: '100%',
    })
    expect(screen.getByText('People added here are not stored.')).toBeInTheDocument()
    expect(
      screen.queryByText('Creating a Teams chat from Wayd is not available yet.'),
    ).not.toBeInTheDocument()
  })

  it('creates the chat with the extra people and does not store them through the link', async () => {
    // Arrange
    const user = userEvent.setup()
    renderChat(
      <IncidentTeamsChat
        incident={incident(null)}
        canUpdate
        employees={[{ value: 'e1', label: 'Casey Submitter' }]}
      />,
    )

    // Act
    await user.click(
      screen.getByRole('button', { name: 'Add Teams chat for this incident' }),
    )
    await user.click(screen.getByRole('combobox'))
    await user.click(screen.getByText('Casey Submitter'))
    await user.click(screen.getByRole('button', { name: 'Create chat' }))

    // Assert
    expect(createChat).toHaveBeenCalledWith({
      id: '01a1d091-2de1-75d3-97d9-34eae573d1fc',
      extraEmployeeIds: ['e1'],
    })
    expect(linkChat).not.toHaveBeenCalled()
  })

  it('opens the stored chat without giving the new tab access back', () => {
    // Arrange
    renderChat(
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
    expect(chat.parentElement?.querySelector('img')).toHaveAttribute(
      'src',
      '/icons/teams-logo.svg',
    )
    expect(
      screen.queryByRole('button', { name: 'Edit Teams chat' }),
    ).not.toBeInTheDocument()
    expect(
      screen.queryByRole('button', { name: 'Remove Teams chat' }),
    ).not.toBeInTheDocument()
  })

  it('puts the Teams logo to the left of the modal title', async () => {
    // Arrange
    const user = userEvent.setup()
    renderChat(
      <IncidentTeamsChat incident={incident(null)} canUpdate employees={[]} />,
    )

    // Act
    await user.click(
      screen.getByRole('button', { name: 'Add Teams chat for this incident' }),
    )

    // Assert
    const title = screen.getByRole('dialog', { name: 'Teams chat' }).querySelector(
      '.ant-modal-title',
    )
    const mark = title?.querySelector('img')
    const label = within(title as HTMLElement).getByText('Teams chat')
    expect(mark).toBeInstanceOf(HTMLImageElement)
    expect(mark).toHaveAttribute('src', '/icons/teams-logo.svg')
    expect(title?.firstElementChild).toHaveStyle({ display: 'inline-flex' })
    expect(
      (mark as HTMLImageElement).compareDocumentPosition(label) &
        Node.DOCUMENT_POSITION_FOLLOWING,
    ).toBe(Node.DOCUMENT_POSITION_FOLLOWING)
  })

  it('uses the Teams logo on the add button', () => {
    // Arrange
    renderChat(
      <IncidentTeamsChat incident={incident(null)} canUpdate employees={[]} />,
    )

    // Act
    const mark = screen
      .getByRole('button', { name: 'Add Teams chat for this incident' })
      .querySelector('img')

    // Assert
    expect(mark).toHaveAttribute('src', '/icons/teams-logo.svg')
    expect(teamsLogo).toContain('fill="#7b83eb"')
    expect(teamsLogo).toContain('M335 349.05')
  })

  it('keeps edit and remove hidden until the row is hovered', () => {
    // Arrange
    renderChat(
      <IncidentTeamsChat
        incident={incident(
          'https://teams.microsoft.com/l/chat/1',
          'Bridge',
        )}
        canUpdate
        employees={[]}
      />,
    )

    // Act
    const actions = screen.getByRole('button', {
      name: 'Edit Teams chat',
    }).parentElement

    // Assert
    expect(actions).toContainElement(
      screen.getByRole('button', { name: 'Remove Teams chat' }),
    )
    expect(actions).toHaveClass(styles.actions)
    expect(actions).toHaveStyle({ marginLeft: 'auto', opacity: '0' })
    expect(actions?.parentElement).toHaveStyle({ width: '100%' })
  })

  it('saves an edited Teams link through the chat endpoint', async () => {
    // Arrange
    const user = userEvent.setup()
    renderChat(
      <IncidentTeamsChat
        incident={incident(
          'https://teams.microsoft.com/l/chat/1',
          'Bridge',
        )}
        canUpdate
        employees={[]}
      />,
    )

    // Act
    await user.click(screen.getByRole('button', { name: 'Edit Teams chat' }))
    const link = screen.getByRole('textbox', { name: 'Teams link' })
    await user.clear(link)
    await user.type(link, 'https://teams.microsoft.com/l/chat/2')
    await user.click(screen.getByRole('button', { name: 'Save' }))

    // Assert
    expect(linkChat).toHaveBeenCalledWith({
      id: '01a1d091-2de1-75d3-97d9-34eae573d1fc',
      link: 'https://teams.microsoft.com/l/chat/2',
      displayName: 'Bridge',
    })
    expect(
      screen.queryByText(
        'Creating a Teams chat from Wayd is not available yet.',
      ),
    ).not.toBeInTheDocument()
  })

  it('removes the stored chat through the chat endpoint', async () => {
    // Arrange
    const user = userEvent.setup()
    renderChat(
      <IncidentTeamsChat
        incident={incident(
          'https://teams.microsoft.com/l/chat/1',
          'Bridge',
        )}
        canUpdate
        employees={[]}
      />,
    )

    // Act
    await user.click(screen.getByRole('button', { name: 'Remove Teams chat' }))
    await user.click(screen.getByRole('button', { name: 'Remove' }))

    // Assert
    expect(linkChat).toHaveBeenCalledWith({
      id: '01a1d091-2de1-75d3-97d9-34eae573d1fc',
      link: '',
    })
  })

  it('shows how to copy a Teams link above the link field', async () => {
    // Arrange
    const user = userEvent.setup()
    renderChat(
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
    expect(screen.getByRole('tabpanel', { name: 'Create' })).not.toHaveTextContent(
      copyLinkSentence,
    )
    await user.click(screen.getByRole('tab', { name: 'Link existing' }))

    // Assert
    const panel = screen.getByRole('tabpanel', { name: 'Link existing' })
    const shot = within(panel).getByRole('img', {
      name: 'Teams menu with Copy link under Mute',
    })
    const steps = within(panel).getByText(copyLinkSentence)
    const field = within(panel).getByRole('textbox', { name: 'Teams link' })
    expect(shot).toHaveAttribute('src', '/images/teams-copy-link-annotated.png')
    expect(shot.compareDocumentPosition(steps) & Node.DOCUMENT_POSITION_FOLLOWING).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    )
    expect(steps.compareDocumentPosition(field) & Node.DOCUMENT_POSITION_FOLLOWING).toBe(
      Node.DOCUMENT_POSITION_FOLLOWING,
    )
    expect(createHash('sha256').update(teamsMenuShot).digest('hex')).toBe(
      teamsMenuShotHash,
    )
  })

  it('accepts a Teams link whose scheme is uppercase', async () => {
    // Arrange
    const user = userEvent.setup()
    renderChat(
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
    renderChat(
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
