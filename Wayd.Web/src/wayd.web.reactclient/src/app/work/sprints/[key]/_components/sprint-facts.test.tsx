import { render, screen } from '@testing-library/react'
import SprintFacts from './sprint-facts'
import { SprintDetailsDto } from '@/src/services/wayd-api'
import dayjs from 'dayjs'

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
  team: { id: 't1', key: 14, name: 'Team Atlas', code: 'AT', type: 'Team' },
  overlapsPreviousSprint: false,
  overlapsNextSprint: false,
  canManageSprint: false,
  canStart: false,
  canComplete: false,
  canReopen: false,
}

// Instants arrive from the API as ISO strings despite the generated type.
const instant = (iso: string) => iso as unknown as Date

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
    expect(screen.getByRole('link', { name: 'Team Atlas' })).toHaveAttribute(
      'href',
      '/organizations/teams/14',
    )
  })

  it('hides the actual rows when the sprint has no active period', () => {
    // Arrange / Act
    render(<SprintFacts sprint={sprint} />)

    // Assert
    expect(screen.queryByText('Actual start')).not.toBeInTheDocument()
    expect(screen.queryByText('Actual end')).not.toBeInTheDocument()
  })

  it('shows a recorded start in the viewer zone and an unrecorded end as a dash', () => {
    // Arrange
    const started = '2026-08-17T15:00:00Z'

    // Act
    render(
      <SprintFacts
        sprint={{
          ...sprint,
          started: instant(started),
          activeFrom: instant(started),
          activeUntil: instant('2026-08-31T05:00:00Z'),
          timeZone: 'America/Chicago',
        }}
      />,
    )

    // Assert
    expect(screen.getByText('Actual start')).toBeInTheDocument()
    expect(
      screen.getByText(dayjs(started).format('MMM D, YYYY h:mm A')),
    ).toBeInTheDocument()
    expect(screen.getByText('Actual end')).toBeInTheDocument()
    expect(screen.getByText('—')).toBeInTheDocument()
  })

  it('shows dashes when the team recorded neither date', () => {
    // Arrange / Act
    render(
      <SprintFacts
        sprint={{
          ...sprint,
          activeFrom: instant('2026-08-17T05:00:00Z'),
          activeUntil: instant('2026-08-31T05:00:00Z'),
          timeZone: 'America/Chicago',
        }}
      />,
    )

    // Assert
    expect(screen.getAllByText('—')).toHaveLength(2)
  })

  it('counts the length over the days the sprint is active, after the actual rows', () => {
    // Arrange — started on Aug 19, runs to the end of its planned Aug 30
    const started = '2026-08-19T15:00:00Z'

    // Act
    const { container } = render(
      <SprintFacts
        sprint={{
          ...sprint,
          started: instant(started),
          activeFrom: instant(started),
          activeUntil: instant('2026-08-31T05:00:00Z'),
          timeZone: 'America/Chicago',
        }}
      />,
    )

    // Assert
    expect(screen.getByText('12 days')).toBeInTheDocument()
    const text = container.textContent ?? ''
    expect(text.indexOf('Actual end')).toBeLessThan(text.indexOf('Length'))
  })
})
