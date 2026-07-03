// The global dayjs mock in jest.setup.ts is a formatting-only shim without real
// date math (subtract/startOf/quarter). The relative-filter builder needs the
// real dayjs, so restore it for this suite.
jest.mock('dayjs', () => jest.requireActual('dayjs'))

import dayjs from 'dayjs'

import {
  RELATIVE_DATE_OPTIONS,
  buildRelativeDateFilter,
} from './date-filter-relative'
import type { DateCondition } from './filter-model'

// Fixed reference: Wednesday, 2026-06-17. dayjs weeks start Sunday by default,
// so "this week" is Sun 2026-06-14 … Sat 2026-06-20. Q2 is Apr 1 … Jun 30.
const REF = dayjs('2026-06-17')

const conditionOf = (period: Parameters<typeof buildRelativeDateFilter>[0]) => {
  const model = buildRelativeDateFilter(period, REF)
  expect(model.type).toBe('date')
  expect(model.conditions).toHaveLength(1)
  return model.conditions[0] as DateCondition
}

describe('buildRelativeDateFilter', () => {
  it('emits an equals condition for single-day periods', () => {
    // Arrange / Act
    const today = conditionOf('today')
    const yesterday = conditionOf('yesterday')
    const tomorrow = conditionOf('tomorrow')

    // Assert
    expect(today).toEqual({ op: 'equals', value: '2026-06-17' })
    expect(yesterday).toEqual({ op: 'equals', value: '2026-06-16' })
    expect(tomorrow).toEqual({ op: 'equals', value: '2026-06-18' })
  })

  it('emits an inRange for this week (Sun–Sat)', () => {
    // Act
    const c = conditionOf('thisWeek')

    // Assert
    expect(c).toEqual({
      op: 'inRange',
      value: '2026-06-14',
      valueTo: '2026-06-20',
    })
  })

  it('emits an inRange for this/last/next month', () => {
    // Assert
    expect(conditionOf('thisMonth')).toEqual({
      op: 'inRange',
      value: '2026-06-01',
      valueTo: '2026-06-30',
    })
    expect(conditionOf('lastMonth')).toEqual({
      op: 'inRange',
      value: '2026-05-01',
      valueTo: '2026-05-31',
    })
    expect(conditionOf('nextMonth')).toEqual({
      op: 'inRange',
      value: '2026-07-01',
      valueTo: '2026-07-31',
    })
  })

  it('emits an inRange for this quarter (Q2 2026)', () => {
    // Act
    const c = conditionOf('thisQuarter')

    // Assert
    expect(c).toEqual({
      op: 'inRange',
      value: '2026-04-01',
      valueTo: '2026-06-30',
    })
  })

  it('emits an inRange for this year', () => {
    // Assert
    expect(conditionOf('thisYear')).toEqual({
      op: 'inRange',
      value: '2026-01-01',
      valueTo: '2026-12-31',
    })
  })

  it('year to date runs from Jan 1 through the reference day', () => {
    // Act
    const c = conditionOf('yearToDate')

    // Assert
    expect(c).toEqual({
      op: 'inRange',
      value: '2026-01-01',
      valueTo: '2026-06-17',
    })
  })

  it('defaults the reference to now when omitted', () => {
    // Act — today with no explicit reference should match now's day
    const model = buildRelativeDateFilter('today')

    // Assert
    expect(model.conditions[0]).toEqual({
      op: 'equals',
      value: dayjs().format('YYYY-MM-DD'),
    })
  })

  it('exposes every period as a labeled option in menu order', () => {
    // Assert — first three are Today/Yesterday/Tomorrow, last is Year to Date
    expect(RELATIVE_DATE_OPTIONS[0]).toEqual({ value: 'today', label: 'Today' })
    expect(RELATIVE_DATE_OPTIONS.at(-1)).toEqual({
      value: 'yearToDate',
      label: 'Year to Date',
    })
    // Every option builds a valid date model.
    for (const opt of RELATIVE_DATE_OPTIONS) {
      expect(buildRelativeDateFilter(opt.value, REF).type).toBe('date')
    }
  })
})
