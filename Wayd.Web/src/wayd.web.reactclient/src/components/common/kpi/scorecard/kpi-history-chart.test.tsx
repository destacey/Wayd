import { KpiPointDto } from '@/src/services/api/wayd.schemas'
import { asCalendarDate, parseCalendarDate } from '@/src/utils/calendar-date'
import { referenceSpan } from './kpi-history-chart'

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
