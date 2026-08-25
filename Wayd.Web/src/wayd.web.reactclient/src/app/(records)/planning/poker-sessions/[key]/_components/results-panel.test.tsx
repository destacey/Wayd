import { render, screen } from '@testing-library/react'
import { PokerVoteDto } from '@/src/services/wayd-api'
import { calculateVoteStats } from './results-panel'
import ResultsPanel from './results-panel'

function makeVote(
  value: string,
  participantId = 'p1',
  participantName = 'User',
): PokerVoteDto {
  return {
    id: `vote-${participantId}-${value}`,
    participant: { id: participantId, userName: participantId, name: participantName },
    value,
    submittedOn: new Date(),
  }
}

describe('calculateVoteStats', () => {
  describe('numeric votes', () => {
    it('calculates average, mode, low, and high', () => {
      const votes = [
        makeVote('1', 'p1'),
        makeVote('3', 'p2'),
        makeVote('5', 'p3'),
        makeVote('3', 'p4'),
      ]
      const stats = calculateVoteStats(votes)

      expect(stats.average).toBe('3.0')
      expect(stats.mode).toBe('3')
      expect(stats.low).toBe('1')
      expect(stats.high).toBe('5')
    })

    it('handles a single vote', () => {
      const votes = [makeVote('8', 'p1')]
      const stats = calculateVoteStats(votes)

      expect(stats.average).toBe('8.0')
      expect(stats.mode).toBe('8')
      expect(stats.low).toBe('8')
      expect(stats.high).toBe('8')
      expect(stats.hasConsensus).toBe(true)
    })

    it('returns consensus when spread is within 3', () => {
      const votes = [
        makeVote('3', 'p1'),
        makeVote('5', 'p2'),
        makeVote('5', 'p3'),
      ]
      const stats = calculateVoteStats(votes)

      expect(stats.hasConsensus).toBe(true)
    })

    it('returns no consensus when spread exceeds 3', () => {
      const votes = [
        makeVote('1', 'p1'),
        makeVote('8', 'p2'),
        makeVote('13', 'p3'),
      ]
      const stats = calculateVoteStats(votes)

      expect(stats.hasConsensus).toBe(false)
    })

    it('handles unanimous numeric votes', () => {
      const votes = [
        makeVote('5', 'p1'),
        makeVote('5', 'p2'),
        makeVote('5', 'p3'),
      ]
      const stats = calculateVoteStats(votes)

      expect(stats.average).toBe('5.0')
      expect(stats.mode).toBe('5')
      expect(stats.low).toBe('5')
      expect(stats.high).toBe('5')
      expect(stats.hasConsensus).toBe(true)
    })
  })

  describe('non-numeric votes', () => {
    it('returns dashes for average, low, and high', () => {
      const votes = [
        makeVote('M', 'p1'),
        makeVote('L', 'p2'),
        makeVote('M', 'p3'),
      ]
      const stats = calculateVoteStats(votes)

      expect(stats.average).toBe('-')
      expect(stats.low).toBe('-')
      expect(stats.high).toBe('-')
    })

    it('calculates mode correctly', () => {
      const votes = [
        makeVote('M', 'p1'),
        makeVote('L', 'p2'),
        makeVote('M', 'p3'),
      ]
      const stats = calculateVoteStats(votes)

      expect(stats.mode).toBe('M')
    })

    it('returns consensus when all votes are the same', () => {
      const votes = [
        makeVote('XL', 'p1'),
        makeVote('XL', 'p2'),
        makeVote('XL', 'p3'),
      ]
      const stats = calculateVoteStats(votes)

      expect(stats.hasConsensus).toBe(true)
    })

    it('returns no consensus when votes differ', () => {
      const votes = [
        makeVote('S', 'p1'),
        makeVote('M', 'p2'),
        makeVote('L', 'p3'),
      ]
      const stats = calculateVoteStats(votes)

      expect(stats.hasConsensus).toBe(false)
    })
  })

  describe('edge cases', () => {
    it('handles empty votes', () => {
      const stats = calculateVoteStats([])

      expect(stats.average).toBe('-')
      expect(stats.mode).toBe('-')
      expect(stats.low).toBe('-')
      expect(stats.high).toBe('-')
      expect(stats.hasConsensus).toBe(false)
    })

    it('handles special numeric values like ?', () => {
      const votes = [
        makeVote('?', 'p1'),
        makeVote('?', 'p2'),
      ]
      const stats = calculateVoteStats(votes)

      expect(stats.average).toBe('-')
      expect(stats.mode).toBe('?')
      expect(stats.hasConsensus).toBe(true)
    })
  })
})

describe('ResultsPanel', () => {
  it('renders nothing when there are no votes', () => {
    const { container } = render(<ResultsPanel votes={[]} />)
    expect(container.firstChild).toBeNull()
  })

  it('displays consensus tag when consensus is reached', () => {
    const votes = [
      makeVote('5', 'p1'),
      makeVote('5', 'p2'),
      makeVote('5', 'p3'),
    ]
    render(<ResultsPanel votes={votes} />)

    expect(screen.getByText('Consensus')).toBeInTheDocument()
    expect(screen.queryByText('Spread — discuss')).not.toBeInTheDocument()
  })

  it('displays spread tag when no consensus', () => {
    const votes = [
      makeVote('1', 'p1'),
      makeVote('13', 'p2'),
    ]
    render(<ResultsPanel votes={votes} />)

    expect(screen.getByText('Spread — discuss')).toBeInTheDocument()
    expect(screen.queryByText('Consensus')).not.toBeInTheDocument()
  })

  it('renders stat values', () => {
    const votes = [
      makeVote('3', 'p1'),
      makeVote('5', 'p2'),
      makeVote('3', 'p3'),
    ]
    render(<ResultsPanel votes={votes} />)

    expect(screen.getByText('Average')).toBeInTheDocument()
    expect(screen.getByText('Mode')).toBeInTheDocument()
    expect(screen.getByText('Low')).toBeInTheDocument()
    expect(screen.getByText('High')).toBeInTheDocument()
  })

  it('renders vote distribution', () => {
    const votes = [
      makeVote('3', 'p1'),
      makeVote('5', 'p2'),
      makeVote('3', 'p3'),
    ]
    render(<ResultsPanel votes={votes} />)

    // Distribution values are rendered in <strong> tags
    const strongElements = document.querySelectorAll('strong')
    const distributionValues = [...strongElements].map((el) => el.textContent)
    expect(distributionValues).toContain('3')
    expect(distributionValues).toContain('5')
  })
})
