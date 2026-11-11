import { PokerRoundDto } from '@/src/services/wayd-api'
import { shownRound } from './shown-round'

const round = (id: string, status: string, order: number): PokerRoundDto =>
  ({ id, status, order, votes: [], voteCount: 0 }) as unknown as PokerRoundDto

const accepted = round('accepted', 'Accepted', 0)
const revealed = round('revealed', 'Revealed', 1)
const voting = round('voting', 'Voting', 2)
const rounds = [accepted, revealed, voting]

describe('shownRound', () => {
  it('shows the round the session is pointed at while following', () => {
    // Act
    const shown = shownRound(rounds, {
      following: true,
      focusedRoundId: 'voting',
      selectedRoundId: 'accepted',
    })

    // Assert
    expect(shown).toBe(voting)
  })

  it('shows the viewer’s own choice when not following', () => {
    // Act
    const shown = shownRound(rounds, {
      following: false,
      focusedRoundId: 'voting',
      selectedRoundId: 'accepted',
    })

    // Assert
    expect(shown).toBe(accepted)
  })

  it.each([
    { following: true, focusedRoundId: undefined, selectedRoundId: 'voting' },
    { following: false, focusedRoundId: 'voting', selectedRoundId: undefined },
  ])(
    'shows the first round in play when nothing is chosen (following: $following)',
    (choice) => {
      // Act
      const shown = shownRound(rounds, choice)

      // Assert
      expect(shown).toBe(revealed)
    },
  )

  it.each([
    { following: true, focusedRoundId: 'removed', selectedRoundId: undefined },
    { following: false, focusedRoundId: undefined, selectedRoundId: 'removed' },
  ])(
    'falls back to the round in play when the chosen round is gone (following: $following)',
    (choice) => {
      // Act
      const shown = shownRound(rounds, choice)

      // Assert
      expect(shown).toBe(revealed)
    },
  )

  it('shows the first round when none is in play', () => {
    // Arrange
    const estimated = [accepted, round('accepted-2', 'Accepted', 1)]

    // Act
    const shown = shownRound(estimated, {
      following: true,
      focusedRoundId: undefined,
      selectedRoundId: undefined,
    })

    // Assert
    expect(shown).toBe(accepted)
  })

  it('shows nothing before the first round', () => {
    // Act
    const shown = shownRound([], {
      following: true,
      focusedRoundId: 'voting',
      selectedRoundId: undefined,
    })

    // Assert
    expect(shown).toBeUndefined()
  })
})
