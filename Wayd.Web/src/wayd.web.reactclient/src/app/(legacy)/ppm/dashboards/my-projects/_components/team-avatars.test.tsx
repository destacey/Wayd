import { render, screen } from '@testing-library/react'
import { userEvent } from '@testing-library/user-event'
import TeamAvatars from './team-avatars'
import { TeamMemberWithRoles } from './project-card-helpers'

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
  return { employee: { id, key: 1, name } as any, roles }
}

describe('TeamAvatars', () => {
  it('renders initials for each member', () => {
    const members = [
      createMember('1', 'Alice Brown', ['Sponsor']),
      createMember('2', 'Bob Smith', ['Owner']),
    ]

    render(<TeamAvatars members={members} />)

    expect(screen.getByText('AB')).toBeInTheDocument()
    expect(screen.getByText('BS')).toBeInTheDocument()
  })

  it('shows tooltip with name and roles on hover', async () => {
    const members = [createMember('1', 'Alice Brown', ['Sponsor', 'PM'])]

    render(<TeamAvatars members={members} />)

    await userEvent.hover(screen.getByText('AB'))

    expect(
      await screen.findByText('Alice Brown (Sponsor, PM)'),
    ).toBeInTheDocument()
  })

  it('shows overflow count when more than 6 members', () => {
    const members = Array.from({ length: 8 }, (_, i) =>
      createMember(`${i}`, `Person ${i}`, ['Member']),
    )

    render(<TeamAvatars members={members} />)

    expect(screen.getByText('+2')).toBeInTheDocument()
  })

  it('does not show overflow for 6 or fewer members', () => {
    const members = Array.from({ length: 6 }, (_, i) =>
      createMember(`${i}`, `Person ${i}`, ['Member']),
    )

    render(<TeamAvatars members={members} />)

    expect(screen.queryByText(/\+/)).not.toBeInTheDocument()
  })

  it('renders empty group when no members', () => {
    const { container } = render(<TeamAvatars members={[]} />)

    expect(container.querySelector('.ant-avatar')).not.toBeInTheDocument()
  })
})
