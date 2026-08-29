import { render, screen } from '@testing-library/react'
import { PokerVoteDto } from '@/src/services/wayd-api'
import ParticipantCards from './participant-cards'

const participants = [
  { id: 'p1', name: 'Alice' },
  { id: 'p2', name: 'Bob' },
  { id: 'p3', name: 'Charlie' },
]

function makeVote(participantId: string, value: string): PokerVoteDto {
  return {
    id: `vote-${participantId}`,
    participant: { id: participantId, userName: participantId, name: participantId },
    value,
    submittedOn: new Date(),
  }
}

describe('ParticipantCards', () => {
  it('renders all participant names', () => {
    render(
      <ParticipantCards
        participants={participants}
        votes={[]}
        isRevealed={false}
      />,
    )

    expect(screen.getByText('Alice')).toBeInTheDocument()
    expect(screen.getByText('Bob')).toBeInTheDocument()
    expect(screen.getByText('Charlie')).toBeInTheDocument()
  })

  it('shows vote values when revealed', () => {
    const votes = [
      makeVote('p1', '5'),
      makeVote('p2', '8'),
    ]

    render(
      <ParticipantCards
        participants={participants}
        votes={votes}
        isRevealed={true}
      />,
    )

    expect(screen.getByText('5')).toBeInTheDocument()
    expect(screen.getByText('8')).toBeInTheDocument()
  })

  it('applies revealed class when isRevealed is true', () => {
    const votes = [makeVote('p1', '5')]

    const { container } = render(
      <ParticipantCards
        participants={participants}
        votes={votes}
        isRevealed={true}
      />,
    )

    const flippedCards = container.querySelectorAll('[class*="cardFlipRevealed"]')
    expect(flippedCards.length).toBe(participants.length)
  })

  it('does not apply revealed class when not revealed', () => {
    const votes = [makeVote('p1', '5')]

    const { container } = render(
      <ParticipantCards
        participants={participants}
        votes={votes}
        isRevealed={false}
      />,
    )

    const flippedCards = container.querySelectorAll('[class*="cardFlipRevealed"]')
    expect(flippedCards.length).toBe(0)
  })

  it('applies voted style for participants who voted', () => {
    const votes = [makeVote('p1', '5')]

    const { container } = render(
      <ParticipantCards
        participants={participants}
        votes={votes}
        isRevealed={false}
      />,
    )

    const votedCards = container.querySelectorAll('[class*="cardFrontVoted"]')
    expect(votedCards.length).toBe(1)

    const pendingCards = container.querySelectorAll('[class*="cardFrontPending"]')
    expect(pendingCards.length).toBe(2)
  })

  it('shows dash for participants who did not vote when revealed', () => {
    const votes = [makeVote('p1', '5')]

    render(
      <ParticipantCards
        participants={participants}
        votes={votes}
        isRevealed={true}
      />,
    )

    // p2 and p3 didn't vote, should show em-dash
    const dashes = screen.getAllByText('—')
    expect(dashes.length).toBe(2)
  })

  it('renders nothing when no participants', () => {
    const { container } = render(
      <ParticipantCards
        participants={[]}
        votes={[]}
        isRevealed={false}
      />,
    )

    const cards = container.querySelectorAll('[class*="cardFlipContainer"]')
    expect(cards.length).toBe(0)
  })
})
