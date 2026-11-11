import { PokerRoundDto } from '@/src/services/wayd-api'

export interface ShownRoundChoice {
  /** Whether this screen follows the facilitator. */
  following: boolean
  /** The round a facilitator pointed the session at, if any. */
  focusedRoundId: string | undefined
  /** The round this viewer chose for themselves, if any. */
  selectedRoundId: string | undefined
}

/**
 * The round a session screen shows: the one the session is pointed at while
 * following the facilitator, otherwise the viewer's own choice. Either falls
 * back to the round in play — the first still voting or revealed, else the
 * first round — when it names no round, or one that has since been removed.
 */
export const shownRound = (
  rounds: PokerRoundDto[],
  { following, focusedRoundId, selectedRoundId }: ShownRoundChoice,
): PokerRoundDto | undefined => {
  const wantedId = following ? focusedRoundId : selectedRoundId
  const wanted = wantedId ? rounds.find((r) => r.id === wantedId) : undefined

  return (
    wanted ??
    rounds.find((r) => r.status === 'Voting' || r.status === 'Revealed') ??
    rounds[0]
  )
}
