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
  team: { id: 't1', key: 14, name: 'Core Services', code: 'CS', type: 'Team' },
  overlapsPreviousSprint: false,
  overlapsNextSprint: false,
  canManageSprint: false,
  canStart: false,
  canComplete: false,
  canReopen: false,
}

const viewerTimeZone = Intl.DateTimeFormat().resolvedOptions().timeZone

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
    expect(screen.getByRole('link', { name: 'Core Services' })).toHaveAttribute(
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

  it('tags recorded moments as actual and implied ones as default', () => {
    // Arrange
    const started = '2026-08-17T15:00:00Z'

    // Act
    render(
      <SprintFacts
        sprint={{
          ...sprint,
          started: instant(started),
          activeFrom: instant(started),
          activeUntil: instant('2026-08-31T04:59:59Z'),
          timeZone: viewerTimeZone,
        }}
      />,
    )

    // Assert
    expect(screen.getByText('Actual start')).toBeInTheDocument()
    expect(screen.getByText('Actual end')).toBeInTheDocument()
    expect(screen.getByText('Actual')).toBeInTheDocument()
    expect(screen.getByText('Default')).toBeInTheDocument()
  })

  it('shows the active moment in the viewer zone', () => {
    // Arrange
    const started = '2026-08-17T15:00:00Z'

    // Act
    render(
      <SprintFacts
        sprint={{
          ...sprint,
          started: instant(started),
          activeFrom: instant(started),
          timeZone: viewerTimeZone,
        }}
      />,
    )

    // Assert
    expect(
      screen.getByText(dayjs(started).format('MMM D, YYYY h:mm A')),
    ).toBeInTheDocument()
  })

  it('names the team time zone when it differs from the viewer', () => {
    // Arrange
    const teamTimeZone =
      viewerTimeZone === 'Pacific/Auckland'
        ? 'Europe/London'
        : 'Pacific/Auckland'

    // Act
    render(
      <SprintFacts
        sprint={{
          ...sprint,
          activeFrom: instant('2026-08-17T15:00:00Z'),
          timeZone: teamTimeZone,
        }}
      />,
    )

    // Assert
    expect(
      screen.getByText(`Team time zone: ${teamTimeZone}`),
    ).toBeInTheDocument()
  })

  it('omits the team time zone when it matches the viewer', () => {
    // Arrange / Act
    render(
      <SprintFacts
        sprint={{
          ...sprint,
          activeFrom: instant('2026-08-17T15:00:00Z'),
          timeZone: viewerTimeZone,
        }}
      />,
    )

    // Assert
    expect(screen.queryByText(/Team time zone/)).not.toBeInTheDocument()
  })
})
