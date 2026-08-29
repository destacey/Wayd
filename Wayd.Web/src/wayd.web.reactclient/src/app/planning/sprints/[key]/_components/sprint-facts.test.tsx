import { render, screen } from '@testing-library/react'
import SprintFacts from './sprint-facts'

jest.unmock('dayjs')
jest.mock('@/src/components/common/links/links-card', () => {
  const LinksCard = () => <div>Links Card</div>
  return LinksCard
})

const sprint = {
  id: 'sprint-1',
  key: 21439,
  name: '26.3.2',
  state: { id: '2', name: 'Active' },
  start: new Date('2026-08-17'),
  end: new Date('2026-08-30'),
  team: { id: 't1', key: 14, name: 'Core Services', type: 'Team' },
} as any

describe('SprintFacts', () => {
  it('renders the boundaries as the calendar dates they are', () => {
    // Arrange / Act — stored as UTC, so formatting locally would shift them a
    // day earlier for anyone behind UTC.
    render(<SprintFacts sprint={sprint} />)

    // Assert
    expect(screen.getByText('Aug 17, 2026')).toBeInTheDocument()
    expect(screen.getByText('Aug 30, 2026')).toBeInTheDocument()
  })

  it('counts the length inclusively', () => {
    // Arrange / Act — Aug 17 to Aug 30 is a fortnight, not 13 days.
    render(<SprintFacts sprint={sprint} />)

    // Assert
    expect(screen.getByText('14 days')).toBeInTheDocument()
  })

  it('says one day rather than 1 days', () => {
    // Arrange / Act
    render(
      <SprintFacts
        sprint={{ ...sprint, start: new Date('2026-08-17'), end: new Date('2026-08-17') }}
      />,
    )

    // Assert
    expect(screen.getByText('1 day')).toBeInTheDocument()
  })

  it('links the team as the sprint container', () => {
    // Arrange / Act
    render(<SprintFacts sprint={sprint} />)

    // Assert
    expect(screen.getByRole('link', { name: 'Core Services' })).toHaveAttribute(
      'href',
      '/organizations/teams/14',
    )
  })
})
