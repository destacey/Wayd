import { SprintBurnDto, SprintScopeMeasureDto } from '@/src/services/wayd-api'

export enum BurnSeries {
  Scope = 'Scope',
  Completed = 'Completed',
  Remaining = 'Remaining',
  Ideal = 'Ideal',
}

/**
 * One point of a burn chart line: a moment, the line it is on, its value, and
 * how the line reaches it. A reading closes a day, so the line steps to it at
 * the start of that day ('vh') and the change shows on the day it happened; the
 * ideal line is a straight reference ('line').
 */
export interface BurnChartPoint {
  at: Date
  series: BurnSeries
  value: number
  shape: 'vh' | 'line'
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
    {
      at: new Date(p.at),
      series: BurnSeries.Scope,
      value: value(p.scope),
      shape: 'vh' as const,
    },
    {
      at: new Date(p.at),
      series: BurnSeries.Completed,
      value: value(p.completed),
      shape: 'vh' as const,
    },
  ])

  const remaining = burn.points.map((p) => ({
    at: new Date(p.at),
    series: BurnSeries.Remaining,
    value: value(p.scope) - value(p.completed),
    shape: 'vh' as const,
  }))

  // Straight from the committed work at the commitment point to zero at the
  // effective end, so evenly over the sprint's calendar time.
  const ideal = [
    {
      at: new Date(burn.effectiveStart),
      series: BurnSeries.Ideal,
      value: value(burn.committed),
      shape: 'line' as const,
    },
    {
      at: new Date(burn.effectiveEnd),
      series: BurnSeries.Ideal,
      value: 0,
      shape: 'line' as const,
    },
  ]

  return { burnUp, burnDown: [...remaining, ...ideal] }
}
