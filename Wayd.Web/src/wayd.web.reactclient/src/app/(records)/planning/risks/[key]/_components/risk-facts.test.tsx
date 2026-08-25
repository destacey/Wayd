import { render, screen } from '@testing-library/react'
import RiskFacts from './risk-facts'

// The global setup mocks dayjs and formats in local time, which is the very
// bug these date assertions exist to catch — a UTC calendar date rendered
// locally shifts a day earlier for anyone behind UTC. Use the real dayjs here.
jest.unmock('dayjs')

jest.mock('@/src/components/common/links/links-card', () => {
  const LinksCard = () => <div>Links Card</div>
  return LinksCard
})

const risk = {
  id: 'risk-1',
  key: 214,
  summary: 'Token refresh may exceed the service cap',
  reportedOn: new Date('2026-03-04'),
  reportedBy: { id: 'e1', key: 1042, name: 'Priya Raghunathan' },
  status: { id: '1', name: 'Open' },
  category: { id: '2', name: 'Technical' },
  impact: { id: '3', name: 'High' },
  likelihood: { id: '4', name: 'Medium' },
  exposure: { id: '5', name: 'High' },
} as any

describe('RiskFacts', () => {
  it('leaves the grading to the matrix in the content column', () => {
    // Arrange / Act — impact and likelihood are the matrix's axes; repeating
    // them here as text would say the same thing twice.
    render(<RiskFacts risk={risk} />)

    // Assert
    expect(screen.queryByText('Impact')).toBeNull()
    expect(screen.queryByText('Likelihood')).toBeNull()
  })

  it('leaves status to the identity bar', () => {
    // Arrange / Act — shown above the panel; repeating it invites the two
    // to disagree.
    render(<RiskFacts risk={risk} />)

    // Assert
    expect(screen.queryByText('Status')).toBeNull()
  })

  it('links a team risk to the teams route', () => {
    // Arrange / Act
    render(
      <RiskFacts
        risk={{
          ...risk,
          team: { id: 't1', key: 14, name: 'Platform Core', type: 'Team' },
        }}
      />,
    )

    // Assert
    expect(screen.getByRole('link', { name: 'Platform Core' })).toHaveAttribute(
      'href',
      '/organizations/teams/14',
    )
  })

  it('links a team-of-teams risk to its own route', () => {
    // Arrange — the two team kinds have different routes, and a risk carries
    // the planning projection rather than a TeamNavigationDto.
    render(
      <RiskFacts
        risk={{
          ...risk,
          team: {
            id: 't2',
            key: 6,
            name: 'Product Engineering',
            type: 'Team of Teams',
          },
        }}
      />,
    )

    // Assert
    expect(
      screen.getByRole('link', { name: 'Product Engineering' }),
    ).toHaveAttribute('href', '/organizations/team-of-teams/6')
  })

  it('says so when nobody is assigned', () => {
    // Arrange / Act — an unassigned risk is worth noticing, not hiding.
    render(<RiskFacts risk={risk} />)

    // Assert
    expect(screen.getByText('Unassigned')).toBeInTheDocument()
  })

  it('omits the follow-up and closed dates when the risk has none', () => {
    // Arrange / Act
    render(<RiskFacts risk={risk} />)

    // Assert
    expect(screen.queryByText('Follow-Up Date')).toBeNull()
    expect(screen.queryByText('Closed')).toBeNull()
  })

  it('shows the closed date once the risk is closed', () => {
    // Arrange / Act
    render(<RiskFacts risk={{ ...risk, closedDate: new Date('2026-05-20') }} />)

    // Assert
    expect(screen.getByText('May 20, 2026')).toBeInTheDocument()
  })

  describe('age', () => {
    // A relative label depends on today, so pin the clock rather than let the
    // expected value drift.
    beforeAll(() => {
      jest.useFakeTimers().setSystemTime(new Date('2026-06-06T12:00:00Z'))
    })
    afterAll(() => jest.useRealTimers())

    it('says how long an open risk has been open', () => {
      // Arrange / Act — reported 2026-03-04, so 94 days by the pinned date.
      render(<RiskFacts risk={risk} />)

      // Assert
      expect(screen.getByText('Open 94 days')).toBeInTheDocument()
    })

    it('stops counting once the risk closes', () => {
      // Arrange / Act — a closed risk was open for a fixed span; counting to
      // today would keep growing forever.
      render(
        <RiskFacts risk={{ ...risk, closedDate: new Date('2026-03-18') }} />,
      )

      // Assert
      expect(screen.getByText('Open 14 days')).toBeInTheDocument()
    })

    it('reads naturally on the day it was reported', () => {
      // Arrange / Act
      render(<RiskFacts risk={{ ...risk, reportedOn: new Date('2026-06-06') }} />)

      // Assert
      expect(screen.getByText('Opened today')).toBeInTheDocument()
    })
  })

  describe('follow-up', () => {
    beforeAll(() => {
      jest.useFakeTimers().setSystemTime(new Date('2026-06-06T12:00:00Z'))
    })
    afterAll(() => jest.useRealTimers())

    it('counts down to a follow-up still ahead', () => {
      // Arrange / Act
      render(
        <RiskFacts risk={{ ...risk, followUpDate: new Date('2026-06-16') }} />,
      )

      // Assert
      expect(screen.getByText('Due in 10 days')).toBeInTheDocument()
    })

    it('calls out an overdue follow-up, which is the actionable state', () => {
      // Arrange / Act
      render(
        <RiskFacts risk={{ ...risk, followUpDate: new Date('2026-05-27') }} />,
      )

      // Assert
      expect(screen.getByText('Overdue by 10 days')).toBeInTheDocument()
    })

    it('says due today rather than "in 0 days"', () => {
      // Arrange / Act
      render(
        <RiskFacts risk={{ ...risk, followUpDate: new Date('2026-06-06') }} />,
      )

      // Assert
      expect(screen.getByText('Due today')).toBeInTheDocument()
    })

    it('drops the countdown once the risk is closed', () => {
      // Arrange / Act — a closed risk is not being chased, so an overdue
      // warning on it would be noise.
      render(
        <RiskFacts
          risk={{
            ...risk,
            followUpDate: new Date('2026-05-27'),
            closedDate: new Date('2026-06-01'),
          }}
        />,
      )

      // Assert
      expect(screen.queryByText(/Overdue/)).toBeNull()
    })
  })
})
