import { SizingMethod, SprintScopeDto } from '@/src/services/wayd-api'
import { SprintMetricFigures } from './sprint-metric-values'
import {
  scopeIsReadable,
  sprintOverviewFigures,
} from './sprint-overview-figures'

const measure = (count: number, estimate: number) => ({ count, estimate })

const metrics: SprintMetricFigures = {
  totalWorkItems: 5,
  totalEstimate: 18,
  completedWorkItems: 2,
  completedEstimate: 8,
  inProgressWorkItems: 2,
  inProgressEstimate: 7,
  notStartedWorkItems: 1,
  notStartedEstimate: 3,
}

const scope: SprintScopeDto = {
  sprintId: 's1',
  sizingMethod: SizingMethod.StoryPoints,
  effectiveStart: '2026-09-15T05:00:00Z' as unknown as Date,
  startIsActual: false,
  effectiveEnd: '2026-09-26T05:00:00Z' as unknown as Date,
  endIsActual: false,
  lastDay: '2026-09-25T05:00:00Z' as unknown as Date,
  timeZone: 'America/Chicago',
  hasTeam: true,
  historyIncomplete: false,
  totals: {
    total: measure(7, 26),
    committed: measure(5, 18),
    added: measure(2, 8),
    completed: measure(3, 11),
    removed: measure(1, 2),
    carriedOver: measure(2, 8),
    descoped: measure(1, 4),
    remaining: measure(1, 3),
    completedOfCommitted: measure(2, 6),
    sayDoCount: 0.4,
    sayDoEstimate: 6 / 18,
    unestimated: 0,
  },
  items: [],
}

const afterCommitment = new Date('2026-09-20T12:00:00Z')

describe('sprintOverviewFigures', () => {
  it('takes completion from scope, measured against everything but descoped work', () => {
    // Arrange / Act
    const result = sprintOverviewFigures(metrics, scope, false, afterCommitment)

    // Assert
    expect(result.completed).toBe(11)
    expect(result.completionBase).toBe(22)
    expect(result.scope).toEqual({
      committed: 18,
      added: 8,
      removed: 2,
      carriedOver: 8,
      descoped: 4,
      sayDo: 6 / 18,
      predictability: 11 / 18,
      reestimated: 0,
      commitment: {
        committed: 18,
        start: new Date('2026-09-15T05:00:00Z'),
        end: new Date('2026-09-26T05:00:00Z'),
      },
    })
  })

  it('reports how much the work grew by being estimated after it came in', () => {
    // Arrange — 26 points came in, and the work is 36 points now
    const grown = { ...scope.totals, total: measure(7, 36) }

    // Act
    const byEstimate = sprintOverviewFigures(
      metrics,
      { ...scope, totals: grown },
      false,
      afterCommitment,
    )
    const byCount = sprintOverviewFigures(
      metrics,
      { ...scope, totals: grown },
      true,
      afterCommitment,
    )

    // Assert
    expect(byEstimate.scope?.reestimated).toBe(10)
    expect(byCount.scope?.reestimated).toBe(0)
  })

  it('caps predictability at 100% when velocity passes the commitment', () => {
    // Arrange / Act
    const result = sprintOverviewFigures(
      metrics,
      { ...scope, totals: { ...scope.totals, completed: measure(6, 25) } },
      false,
      afterCommitment,
    )

    // Assert
    expect(result.scope?.predictability).toBe(1)
  })

  it('has no predictability when nothing was committed', () => {
    // Arrange / Act
    const result = sprintOverviewFigures(
      metrics,
      { ...scope, totals: { ...scope.totals, committed: measure(0, 0) } },
      false,
      afterCommitment,
    )

    // Assert
    expect(result.scope?.predictability).toBeNull()
  })

  it('takes work in progress and not started from the current items', () => {
    // Arrange / Act
    const result = sprintOverviewFigures(metrics, scope, false, afterCommitment)

    // Assert
    expect(result.inProgress).toBe(7)
    expect(result.notStarted).toBe(3)
    expect(result.currentTotal).toBe(18)
  })

  it('counts items when asked', () => {
    // Arrange / Act
    const result = sprintOverviewFigures(metrics, scope, true, afterCommitment)

    // Assert
    expect(result.completed).toBe(3)
    expect(result.completionBase).toBe(6)
    expect(result.inProgress).toBe(2)
    expect(result.scope?.sayDo).toBe(0.4)
  })

  it('falls back to the current items while history is incomplete', () => {
    // Arrange / Act
    const result = sprintOverviewFigures(
      metrics,
      { ...scope, historyIncomplete: true },
      false,
      afterCommitment,
    )

    // Assert
    expect(result.completed).toBe(8)
    expect(result.completionBase).toBe(18)
    expect(result.scope).toBeNull()
  })

  it('falls back to the current items before the commitment point', () => {
    // Arrange / Act
    const result = sprintOverviewFigures(
      metrics,
      scope,
      false,
      new Date('2026-09-14T20:00:00Z'),
    )

    // Assert
    expect(result.scope).toBeNull()
    expect(result.completed).toBe(8)
  })
})

describe('scopeIsReadable', () => {
  it('is false without a scope', () => {
    // Arrange / Act / Assert
    expect(scopeIsReadable(undefined, afterCommitment)).toBe(false)
  })
})
