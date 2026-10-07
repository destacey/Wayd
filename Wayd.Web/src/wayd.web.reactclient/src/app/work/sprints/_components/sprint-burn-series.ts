import { SprintBurnDto, SprintScopeMeasureDto } from '@/src/services/wayd-api'

export enum BurnSeries {
  Scope = 'Scope',
  Completed = 'Completed',
  Remaining = 'Remaining',
  Ideal = 'Ideal',
}

/** One point of a burn chart line: a moment, the line it is on, and its value. */
export interface BurnChartPoint {
  at: Date
  series: BurnSeries
  value: number
}

export interface BurnChartSeries {
  /** Scope and Completed at each reading. */
  burnUp: BurnChartPoint[]
  /** Remaining at each reading, and the ideal line from committed work to zero. */
  burnDown: BurnChartPoint[]
}

/**
 * The lines of a sprint's burn-up and burn-down, by estimate or by item count.
 * Both units come in every reading, so switching unit needs no refetch.
 */
export const sprintBurnSeries = (
  burn: SprintBurnDto | undefined,
  byCount: boolean,
): BurnChartSeries => {
  if (!burn || burn.points.length === 0) return { burnUp: [], burnDown: [] }

  const value = (m: SprintScopeMeasureDto) => (byCount ? m.count : m.estimate)

  const burnUp = burn.points.flatMap((p) => [
    { at: new Date(p.at), series: BurnSeries.Scope, value: value(p.scope) },
    {
      at: new Date(p.at),
      series: BurnSeries.Completed,
      value: value(p.completed),
    },
  ])

  const remaining = burn.points.map((p) => ({
    at: new Date(p.at),
    series: BurnSeries.Remaining,
    value: value(p.scope) - value(p.completed),
  }))

  // Straight from the committed work at the commitment point to zero at the
  // effective end, so evenly over the sprint's calendar time.
  const ideal = [
    {
      at: new Date(burn.effectiveStart),
      series: BurnSeries.Ideal,
      value: value(burn.committed),
    },
    { at: new Date(burn.effectiveEnd), series: BurnSeries.Ideal, value: 0 },
  ]

  return { burnUp, burnDown: [...remaining, ...ideal] }
}
