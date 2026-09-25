import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import TeamAvatars from './team-avatars'
import { TeamMemberWithRoles } from './dashboard-model'

global.ResizeObserver = class {
  observe() {}
  unobserve() {}
  disconnect() {}
} as unknown as typeof ResizeObserver

function createMember(
  id: string,
  name: string,
  roles: string[],
): TeamMemberWithRoles {
  return { employee: { id, key: 1, name }, roles }
}

describe('TeamAvatars', () => {
  it('renders initials for each member', () => {
    // Arrange
    const members = [
      createMember('1', 'Alice Brown', ['Sponsor']),
      createMember('2', 'Bob Smith', ['Owner']),
    ]

    // Act
    render(<TeamAvatars members={members} />)

    // Assert
    expect(screen.getByText('AB')).toBeInTheDocument()
    expect(screen.getByText('BS')).toBeInTheDocument()
  })

  it('shows the name and roles on hover', async () => {
    // Arrange
    const members = [createMember('1', 'Alice Brown', ['Sponsor', 'PM'])]
    render(<TeamAvatars members={members} />)

    // Act
    await userEvent.hover(screen.getByText('AB'))

    // Assert
    expect(
      await screen.findByText('Alice Brown (Sponsor, PM)'),
    ).toBeInTheDocument()
  })

  it('collapses members beyond the limit into an overflow count that names them on hover', async () => {
    // Arrange
    const members = [
      createMember('1', 'Alice Brown', ['Owner']),
      createMember('2', 'Bob Smith', ['Owner']),
      createMember('3', 'Cy Doe', ['PM']),
      createMember('4', 'Di Eve', ['PM']),
    ]

    // Act
    render(<TeamAvatars members={members} max={3} />)

    // Assert
    expect(screen.getByText('+1')).toBeInTheDocument()
    expect(screen.queryByText('DE')).not.toBeInTheDocument()

    await userEvent.hover(screen.getByText('+1'))
    expect(await screen.findByText('Di Eve (PM)')).toBeInTheDocument()
  })
})
