import { SizingMethod, SprintBurnDto } from '@/src/services/wayd-api'
import { BurnSeries, sprintBurnSeries } from './sprint-burn-series'

const measure = (count: number, estimate: number) => ({ count, estimate })

const burn = {
  sprintId: 's1',
  sizingMethod: SizingMethod.StoryPoints,
  effectiveStart: '2026-09-15T05:00:00Z',
  effectiveEnd: '2026-09-26T05:00:00Z',
  timeZone: 'America/Chicago',
  historyIncomplete: false,
  committed: measure(4, 20),
  points: [
    {
      at: '2026-09-15T05:00:00Z',
      day: '2026-09-15',
      scope: measure(4, 20),
      completed: measure(0, 0),
    },
    {
      at: '2026-09-16T05:00:00Z',
      day: '2026-09-15',
      scope: measure(5, 23),
      completed: measure(1, 5),
    },
  ],
} as unknown as SprintBurnDto

const valuesOf = (
  points: ReturnType<typeof sprintBurnSeries>['burnUp'],
  series: BurnSeries,
) => points.filter((p) => p.series === series).map((p) => p.value)

describe('sprintBurnSeries', () => {
  it('draws scope and completed for the burn-up', () => {
    // Arrange / Act
    const result = sprintBurnSeries(burn, false)

    // Assert
    expect(valuesOf(result.burnUp, BurnSeries.Scope)).toEqual([20, 23])
    expect(valuesOf(result.burnUp, BurnSeries.Completed)).toEqual([0, 5])
  })

  it('draws remaining, and an ideal line from committed to zero, for the burn-down', () => {
    // Arrange / Act
    const result = sprintBurnSeries(burn, false)

    // Assert
    expect(valuesOf(result.burnDown, BurnSeries.Remaining)).toEqual([20, 18])
    const ideal = result.burnDown.filter((p) => p.series === BurnSeries.Ideal)
    expect(ideal.map((p) => [p.at.toISOString(), p.value])).toEqual([
      ['2026-09-15T05:00:00.000Z', 20],
      ['2026-09-26T05:00:00.000Z', 0],
    ])
  })

  it('counts items when asked, from the same readings', () => {
    // Arrange / Act
    const result = sprintBurnSeries(burn, true)

    // Assert
    expect(valuesOf(result.burnUp, BurnSeries.Scope)).toEqual([4, 5])
    expect(valuesOf(result.burnDown, BurnSeries.Remaining)).toEqual([4, 4])
    expect(valuesOf(result.burnDown, BurnSeries.Ideal)).toEqual([4, 0])
  })

  it('draws nothing before the commitment point', () => {
    // Arrange / Act
    const result = sprintBurnSeries({ ...burn, points: [] }, false)

    // Assert
    expect(result).toEqual({ burnUp: [], burnDown: [] })
  })
})
