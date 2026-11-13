import {
  PokerRoundDto,
  PokerRoundSubjectDto,
  PokerRoundSubjectKind,
} from '@/src/services/wayd-api'

/**
 * What a round is about, as one line: its label, led by the key of the record
 * it estimates when it is about one. Undefined for a round that names nothing
 * yet.
 */
export const roundTitle = (round: PokerRoundDto): string | undefined => {
  const label = round.label?.trim() || undefined
  if (!round.subject) return label
  return label
    ? `${round.subject.displayKey} · ${label}`
    : round.subject.displayKey
}

/**
 * The page of the record a round estimates, or undefined for a kind of record
 * with no page to open.
 */
export const subjectHref = (
  subject: PokerRoundSubjectDto,
): string | undefined =>
  subject.kind === PokerRoundSubjectKind.IntakeRequest
    ? `/demand-management/requests/${subject.displayKey}`
    : undefined
