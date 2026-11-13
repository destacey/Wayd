import { PokerRoundDto, PokerRoundSubjectKind } from '@/src/services/wayd-api'
import { roundTitle } from './round-title'

const aRound = (overrides: Partial<PokerRoundDto> = {}): PokerRoundDto => ({
  id: 'round-1',
  status: 'Voting',
  order: 0,
  voteCount: 0,
  votes: [],
  ...overrides,
})

describe('roundTitle', () => {
  it('names the record a round estimates', () => {
    const round = aRound({
      label: 'ignored',
      subject: {
        kind: PokerRoundSubjectKind.IntakeRequest,
        id: 'request-1',
        displayKey: '42',
        title: 'Export invoices',
      },
    })

    expect(roundTitle(round)).toBe('42 · Export invoices')
  })

  it('falls back to the free-text label', () => {
    expect(roundTitle(aRound({ label: ' Story 7 ' }))).toBe('Story 7')
  })

  it('is undefined for a round that names nothing', () => {
    expect(roundTitle(aRound({ label: '  ' }))).toBeUndefined()
    expect(roundTitle(aRound())).toBeUndefined()
  })
})
