import { render, screen } from '@testing-library/react'
import SprintFacts from './sprint-facts'
import { SprintDetailsDto } from '@/src/services/wayd-api'

jest.unmock('dayjs')
jest.mock('@/src/components/common/links/links-card', () => {
  const LinksCard = () => <div>Links Card</div>
  return LinksCard
})

const sprint: SprintDetailsDto = {
  id: 'sprint-1',
  key: 21439,
  name: '26.3.2',
  state: { id: 2, name: 'Active' },
  start: '2026-08-17',
  end: '2026-08-30',
  team: { id: 't1', key: 14, name: 'Core Services', code: 'CS', type: 'Team' },
}

describe('SprintFacts', () => {
  it('renders the boundaries as the calendar dates they are', () => {
    // Arrange / Act — a calendar date has no zone, so no browser zone may
    // shift it.
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
        sprint={{ ...sprint, start: '2026-08-17', end: '2026-08-17' }}
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
