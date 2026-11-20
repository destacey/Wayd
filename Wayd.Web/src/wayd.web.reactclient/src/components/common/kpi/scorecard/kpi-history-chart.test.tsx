import { KpiPointDto } from '@/src/services/api/wayd.schemas'
import { asCalendarDate, parseCalendarDate } from '@/src/utils/calendar-date'
import {
  KpiSeries,
  planSteps,
  referenceSpan,
  widenToPlan,
} from './kpi-history-chart'

const point = (periodStart: string, periodEnd: string): KpiPointDto => ({
  periodStart: asCalendarDate(periodStart),
  periodEnd: asCalendarDate(periodEnd),
  value: 1,
})

const at = (p: KpiPointDto) => parseCalendarDate(p.periodEnd).toDate()
const day = (date: string) => parseCalendarDate(asCalendarDate(date)).toDate()

describe('referenceSpan', () => {
  it('runs from the first measurement to the last', () => {
    const first = point('2026-10-01', '2026-10-31')
    const last = point('2026-11-01', '2026-11-30')

    expect(referenceSpan(first, last, at)).toEqual([
      day('2026-10-31'),
      day('2026-11-30'),
    ])
  })

  it('spans the period of a single measurement, so the line has two ends', () => {
    const only = point('2026-11-01', '2026-11-15')

    expect(referenceSpan(only, only, at)).toEqual([
      day('2026-11-01'),
      day('2026-11-15'),
    ])
  })

  it('reaches back a day for a single measurement of one day', () => {
    const only = point('2026-11-15', '2026-11-15')

    expect(referenceSpan(only, only, at)).toEqual([
      day('2026-11-14'),
      day('2026-11-15'),
    ])
  })
})

describe('planSteps', () => {
  const q1 = {
    date: asCalendarDate('2026-03-31'),
    label: 'Q1',
    plannedValue: 12,
    atRiskValue: 11,
  }
  const q2 = {
    date: asCalendarDate('2026-06-30'),
    label: 'Q2',
    plannedValue: 15,
  }

  it('holds each planned value from the day after the checkpoint before to its own date', () => {
    // Act
    const plan = planSteps([q2, q1], day('2026-01-01')).filter(
      (p) => p.series === KpiSeries.Plan,
    )

    // Assert
    expect(plan.map((p) => [p.at, p.value])).toEqual([
      [day('2026-01-01'), 12],
      [day('2026-03-31'), 12],
      [day('2026-04-01'), 15],
      [day('2026-06-30'), 15],
    ])
  })

  it('breaks the at-risk line where a checkpoint has none', () => {
    // Act
    const atRisk = planSteps([q1, q2], day('2026-01-01')).filter(
      (p) => p.series === KpiSeries.PlanAtRisk,
    )

    // Assert
    expect(atRisk.map((p) => p.value)).toEqual([11, 11, null])
  })

  it('draws a first checkpoint dated before the plan starts as a one-day step', () => {
    // Act
    const plan = planSteps([q1], day('2026-05-01')).filter(
      (p) => p.series === KpiSeries.Plan,
    )

    // Assert
    expect(plan.map((p) => p.at)).toEqual([
      day('2026-03-30'),
      day('2026-03-31'),
    ])
  })

  it('draws no at-risk line when no checkpoint has one', () => {
    // Act
    const steps = planSteps([q2], undefined)

    // Assert
    expect(steps.every((p) => p.series === KpiSeries.Plan)).toBe(true)
    expect(steps.map((p) => p.at)).toEqual([
      day('2026-06-29'),
      day('2026-06-30'),
    ])
  })
})

describe('widenToPlan', () => {
  it('reaches to the last checkpoint when the plan runs past the measurements', () => {
    // Arrange
    const plan = planSteps(
      [{ date: asCalendarDate('2027-03-31'), label: 'Q1', plannedValue: 25 }],
      day('2026-10-31'),
    )

    // Act
    const span = widenToPlan([day('2026-08-31'), day('2026-10-31')], plan)

    // Assert
    expect(span).toEqual([day('2026-08-31'), day('2027-03-31')])
  })

  it('keeps the span when there is no plan', () => {
    // Act
    const span = widenToPlan([day('2026-08-31'), day('2026-10-31')], [])

    // Assert
    expect(span).toEqual([day('2026-08-31'), day('2026-10-31')])
  })
})
