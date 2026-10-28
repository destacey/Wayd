import {
  IntakeRequestCurrentScoreDto,
  IntakeRequestSummaryDto,
} from '@/src/services/wayd-api'
import { orderRequests, toRequestScores } from './request-scores'

const request = (id: string, key: number): IntakeRequestSummaryDto =>
  ({ id, key, title: id }) as IntakeRequestSummaryDto

const score = (
  requestId: string,
  value: number,
  isDeskModel = true,
): IntakeRequestCurrentScoreDto =>
  ({
    requestId,
    value,
    isDeskModel,
    scoredOn: new Date('2026-10-01T00:00:00Z'),
    scoringModel: { id: 'm-1', key: 1, name: 'WSJF' },
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
})
