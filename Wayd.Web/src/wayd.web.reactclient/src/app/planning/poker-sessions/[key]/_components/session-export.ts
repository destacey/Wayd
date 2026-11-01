import { caseInsensitiveCompare } from '@/src/components/common/wayd-grid'
import { PokerRoundDto, PokerVoteDto } from '@/src/services/wayd-api'

/**
 * Flattening a poker session to CSV: one row per round, in the order the rounds were added.
 *
 * Votes are written only once a round is revealed. While a round is Voting the API returns every
 * value but the viewer's own as blank, so writing them would record a half-empty, misleading tally.
 */

export const EXPORT_HEADERS = [
  'Round',
  'Label',
  'Description',
  'Status',
  'Consensus Estimate',
  'Vote Count',
  'Votes',
]

/** Comma is the delimiter, so a multi-value cell separates with semicolons. */
const MULTI_VALUE_SEPARATOR = '; '

const voterName = (vote: PokerVoteDto): string =>
  vote.participant?.name || vote.participant?.userName || 'Unknown'

const formatVotes = (round: PokerRoundDto): string => {
  if (round.status === 'Voting') return ''
  return [...round.votes]
    .sort((a, b) => caseInsensitiveCompare(voterName(a), voterName(b)))
    .map((v) => `${voterName(v)}: ${v.value}`)
    .join(MULTI_VALUE_SEPARATOR)
}

export const buildExportRows = (rounds: PokerRoundDto[]): unknown[][] =>
  [...rounds]
    .sort((a, b) => a.order - b.order)
    .map((round, index) => [
      index + 1,
      round.label ?? '',
      round.description ?? '',
      round.status,
      round.consensusEstimate ?? '',
      round.voteCount,
      formatVotes(round),
    ])
