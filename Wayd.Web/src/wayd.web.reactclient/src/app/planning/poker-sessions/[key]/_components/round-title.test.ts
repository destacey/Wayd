import { PokerRoundDto, PokerRoundSubjectKind } from '@/src/services/wayd-api'
import { roundTitle, subjectHref } from './round-title'

const aRound = (overrides: Partial<PokerRoundDto> = {}): PokerRoundDto => ({
  id: 'round-1',
  status: 'Voting',
  order: 0,
  voteCount: 0,
  votes: [],
  ...overrides,
})

describe('roundTitle', () => {
  const request = {
    kind: PokerRoundSubjectKind.IntakeRequest,
    id: 'request-1',
    displayKey: '42',
  }

  it('leads the label of a round about a record with the record’s key', () => {
    const round = aRound({ label: 'Export invoices', subject: request })

    expect(roundTitle(round)).toBe('42 · Export invoices')
  })

  it('names a round about a record by its key alone when it has no label', () => {
    expect(roundTitle(aRound({ subject: request }))).toBe('42')
  })

  it('falls back to the free-text label', () => {
    expect(roundTitle(aRound({ label: ' Story 7 ' }))).toBe('Story 7')
  })

  it('is undefined for a round that names nothing', () => {
    expect(roundTitle(aRound({ label: '  ' }))).toBeUndefined()
    expect(roundTitle(aRound())).toBeUndefined()
  })
})

describe('subjectHref', () => {
  it('leads an intake request to its page by its number', () => {
    // Act
    const href = subjectHref({
      kind: PokerRoundSubjectKind.IntakeRequest,
      id: 'r-42',
      displayKey: '42',
    })

    // Assert
    expect(href).toBe('/demand-management/requests/42')
  })
})
