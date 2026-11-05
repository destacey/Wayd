import { DependencyAnalysisPeriodDto } from '@/src/services/wayd-api'
import {
  buildFlowData,
  buildOpenData,
  describeFlow,
  isPartialPeriod,
  periodDates,
} from './dependency-analysis-over-time-data'

jest.unmock('dayjs')

const period = (
  start: string,
  end: string,
  counts: Partial<DependencyAnalysisPeriodDto> = {},
): DependencyAnalysisPeriodDto => ({
  start,
  end,
  created: 0,
  closed: 0,
  open: 0,
  ...counts,
})

describe('isPartialPeriod', () => {
  it('marks a two-week period cut short by the end of the range', () => {
    // Arrange
    const periods = [
      period('2026-07-01', '2026-07-14'),
      period('2026-07-15', '2026-07-28'),
      period('2026-07-29', '2026-08-03'),
    ]

    // Act
    const partial = periods.map((p) => isPartialPeriod(p, periods))

    // Assert
    expect(partial).toEqual([false, false, true])
  })

  it('marks months cut short at either end of the range', () => {
    // Arrange
    const periods = [
      period('2026-01-15', '2026-01-31'),
      period('2026-02-01', '2026-02-28'),
      period('2026-03-01', '2026-03-31'),
      period('2026-04-01', '2026-04-10'),
    ]

    // Act
    const partial = periods.map((p) => isPartialPeriod(p, periods))

    // Assert
    expect(partial).toEqual([true, false, false, true])
  })
})

describe('periodDates', () => {
  it('names a single day once and a span by its ends', () => {
    // Act
    const single = periodDates(period('2026-07-29', '2026-07-29'))
    const span = periodDates(period('2026-07-01', '2026-07-14'))

    // Assert
    expect(single).toBe('Jul 29, 2026')
    expect(span).toBe('Jul 1 – Jul 14, 2026')
  })
})

describe('buildOpenData and buildFlowData', () => {
  const periods = [
    period('2026-07-01', '2026-07-14', { created: 4, closed: 1, open: 6 }),
    period('2026-07-15', '2026-07-20', { created: 2, closed: 3, open: 5 }),
  ]

  it('plots each period at its first day, marking the partial one', () => {
    // Act
    const data = buildOpenData(periods)

    // Assert
    expect(data).toEqual([
      { period: 'Jul 1, 2026', dates: 'Jul 1 – Jul 14, 2026', open: 6 },
      {
        period: 'Jul 15, 2026 (partial)',
        dates: 'Jul 15 – Jul 20, 2026',
        open: 5,
      },
    ])
  })

  it('gives each period a created bar then a closed bar', () => {
    // Act
    const data = buildFlowData(periods)

    // Assert
    expect(data.map((d) => [d.period, d.series, d.count])).toEqual([
      ['Jul 1, 2026', 'Created', 4],
      ['Jul 1, 2026', 'Closed', 1],
      ['Jul 15, 2026 (partial)', 'Created', 2],
      ['Jul 15, 2026 (partial)', 'Closed', 3],
    ])
  })

  it('describes the flow for screen readers', () => {
    // Act
    const description = describeFlow(periods)

    // Assert
    expect(description).toBe(
      'Dependencies created and closed in each period: Jul 1 – Jul 14, 2026, 4 created, 1 closed; Jul 15 – Jul 20, 2026, 2 created, 3 closed.',
    )
  })
})
