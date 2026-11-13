import { PokerRoundDto } from '@/src/services/wayd-api'

/**
 * What a round is about, as one line: the record it estimates, or its free-text
 * label. Undefined for a round that names nothing yet.
 */
export const roundTitle = (round: PokerRoundDto): string | undefined => {
  if (round.subject)
    return `${round.subject.displayKey} · ${round.subject.title}`
  return round.label?.trim() || undefined
}
