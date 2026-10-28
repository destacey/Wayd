import {
  IntakeRequestCurrentScoreDto,
  IntakeRequestSummaryDto,
} from '@/src/services/wayd-api'
import { orderRequests, toRequestScores } from './request-scores'

const request = (id: string, key: number): IntakeRequestSummaryDto =>
  ({ id, key, title: id }) as IntakeRequestSummaryDto

const wsjf = { id: 'm-1', key: 1, name: 'WSJF' }

const score = (
  requestId: string,
  value: number,
  isDeskModel = true,
  scoringModel = wsjf,
): IntakeRequestCurrentScoreDto =>
  ({
    requestId,
    value,
    isDeskModel,
    scoredOn: new Date('2026-10-01T00:00:00Z'),
    scoringModel,
  }) as IntakeRequestCurrentScoreDto

describe('orderRequests', () => {
  const requests = [
    request('unscored', 1),
    request('low', 2),
    request('stale', 3),
    request('high', 4),
    request('high-older', 5),
  ]
  const scores = toRequestScores([
    score('low', 1.6),
    score('stale', 9, false),
    score('high', 3.3),
    score('high-older', 3.3),
  ])

  it('ranks by score, highest first, with stale and unscored requests after', () => {
    // Act
    const ordered = orderRequests(requests, 'score', scores)

    // Assert
    expect(ordered.map((r) => r.id)).toEqual([
      'high-older',
      'high',
      'low',
      'stale',
      'unscored',
    ])
  })

  it('leaves the usual order as it is', () => {
    // Act
    const ordered = orderRequests(requests, 'usual', scores)

    // Assert
    expect(ordered).toBe(requests)
  })

  it('ranks scores from different models in separate runs rather than by raw value', () => {
    // Arrange — WSJF runs to about 50 and the weighted model to about 5, so their values do not compare
    const weighted = { id: 'm-2', key: 2, name: 'Weighted Criteria' }
    const mixed = [
      request('wsjf-low', 1),
      request('weighted-high', 2),
      request('wsjf-high', 3),
    ]
    const mixedScores = toRequestScores([
      score('wsjf-low', 10),
      score('weighted-high', 4.5, true, weighted),
      score('wsjf-high', 40),
    ])

    // Act
    const ordered = orderRequests(mixed, 'score', mixedScores)

    // Assert
    expect(ordered.map((r) => r.id)).toEqual([
      'weighted-high',
      'wsjf-high',
      'wsjf-low',
    ])
  })
})
