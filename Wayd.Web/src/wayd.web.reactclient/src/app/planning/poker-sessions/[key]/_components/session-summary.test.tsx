import { render, screen } from '@testing-library/react'
import { PokerRoundDto } from '@/src/services/wayd-api'
import SessionSummary from './session-summary'

function makeRound(
  status: string,
  overrides?: Partial<PokerRoundDto>,
): PokerRoundDto {
  return {
    id: `round-${Math.random()}`,
    label: 'Test',
    status,
    order: 1,
    voteCount: 0,
    votes: [],
    ...overrides,
  }
}

describe('SessionSummary', () => {
  it('displays 0 / 0 when no rounds exist', () => {
    render(<SessionSummary rounds={[]} />)

    expect(screen.getByText('Completed')).toBeInTheDocument()
    expect(screen.getByText('0 / 0')).toBeInTheDocument()
  })

  it('displays completed count out of total', () => {
    const rounds = [
      makeRound('Accepted'),
      makeRound('Accepted'),
      makeRound('Voting'),
      makeRound('Revealed'),
    ]
    render(<SessionSummary rounds={rounds} />)

    expect(screen.getByText('2 / 4')).toBeInTheDocument()
  })

  it('displays all completed when every round is accepted', () => {
    const rounds = [
      makeRound('Accepted'),
      makeRound('Accepted'),
      makeRound('Accepted'),
    ]
    render(<SessionSummary rounds={rounds} />)

    expect(screen.getByText('3 / 3')).toBeInTheDocument()
  })

  it('displays 0 completed when no rounds are accepted', () => {
    const rounds = [
      makeRound('Voting'),
      makeRound('Revealed'),
    ]
    render(<SessionSummary rounds={rounds} />)

    expect(screen.getByText('0 / 2')).toBeInTheDocument()
  })
})
