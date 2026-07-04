/**
 * Relative date quick-filters for the WaydGrid date filter panel — the "Date
 * Filters" submenu in Excel (Today, This Week, Last Month, Year to Date, …).
 *
 * Each period resolves, *at click time*, to a concrete {@link DateFilterModel}
 * against a reference "now". Storing an absolute range (rather than the relative
 * token) keeps the descriptor engine-agnostic: it evaluates through the same
 * `inRange` / `after` / `before` path as a hand-built date condition, and the
 * value is stable once chosen (it doesn't drift as the clock moves). This mirrors
 * how Excel materializes a relative filter into a fixed range.
 *
 * Pure and dayjs-based so it is unit-testable with an injected reference date.
 */
import dayjs from 'dayjs'
import quarterOfYear from 'dayjs/plugin/quarterOfYear'

import type { DateCondition, DateFilterModel } from './filter-model'

// `startOf('quarter')` / `endOf('quarter')` require this plugin; dayjs (and the
// copy antd bundles) don't load it by default. Extending is idempotent.
dayjs.extend(quarterOfYear)

/** Identifiers for the supported relative periods, in menu order. */
export type RelativeDatePeriod =
  | 'today'
  | 'yesterday'
  | 'tomorrow'
  | 'thisWeek'
  | 'lastWeek'
  | 'nextWeek'
  | 'thisMonth'
  | 'lastMonth'
  | 'nextMonth'
  | 'thisQuarter'
  | 'lastQuarter'
  | 'nextQuarter'
  | 'thisYear'
  | 'lastYear'
  | 'nextYear'
  | 'yearToDate'

export interface RelativeDateOption {
  value: RelativeDatePeriod
  label: string
}

/** The relative-period options, in the order they appear in the menu. */
export const RELATIVE_DATE_OPTIONS: RelativeDateOption[] = [
  { value: 'today', label: 'Today' },
  { value: 'yesterday', label: 'Yesterday' },
  { value: 'tomorrow', label: 'Tomorrow' },
  { value: 'thisWeek', label: 'This Week' },
  { value: 'lastWeek', label: 'Last Week' },
  { value: 'nextWeek', label: 'Next Week' },
  { value: 'thisMonth', label: 'This Month' },
  { value: 'lastMonth', label: 'Last Month' },
  { value: 'nextMonth', label: 'Next Month' },
  { value: 'thisQuarter', label: 'This Quarter' },
  { value: 'lastQuarter', label: 'Last Quarter' },
  { value: 'nextQuarter', label: 'Next Quarter' },
  { value: 'thisYear', label: 'This Year' },
  { value: 'lastYear', label: 'Last Year' },
  { value: 'nextYear', label: 'Next Year' },
  { value: 'yearToDate', label: 'Year to Date' },
]

const YMD = 'YYYY-MM-DD'

/** Inclusive [start, end] day range for a period, relative to `ref`. */
const rangeFor = (
  period: RelativeDatePeriod,
  ref: dayjs.Dayjs,
): { start: dayjs.Dayjs; end: dayjs.Dayjs } => {
  switch (period) {
    case 'today':
      return { start: ref, end: ref }
    case 'yesterday': {
      const d = ref.subtract(1, 'day')
      return { start: d, end: d }
    }
    case 'tomorrow': {
      const d = ref.add(1, 'day')
      return { start: d, end: d }
    }
    case 'thisWeek':
      return { start: ref.startOf('week'), end: ref.endOf('week') }
    case 'lastWeek': {
      const w = ref.subtract(1, 'week')
      return { start: w.startOf('week'), end: w.endOf('week') }
    }
    case 'nextWeek': {
      const w = ref.add(1, 'week')
      return { start: w.startOf('week'), end: w.endOf('week') }
    }
    case 'thisMonth':
      return { start: ref.startOf('month'), end: ref.endOf('month') }
    case 'lastMonth': {
      const m = ref.subtract(1, 'month')
      return { start: m.startOf('month'), end: m.endOf('month') }
    }
    case 'nextMonth': {
      const m = ref.add(1, 'month')
      return { start: m.startOf('month'), end: m.endOf('month') }
    }
    case 'thisQuarter':
      return { start: ref.startOf('quarter'), end: ref.endOf('quarter') }
    case 'lastQuarter': {
      const q = ref.subtract(1, 'quarter')
      return { start: q.startOf('quarter'), end: q.endOf('quarter') }
    }
    case 'nextQuarter': {
      const q = ref.add(1, 'quarter')
      return { start: q.startOf('quarter'), end: q.endOf('quarter') }
    }
    case 'thisYear':
      return { start: ref.startOf('year'), end: ref.endOf('year') }
    case 'lastYear': {
      const y = ref.subtract(1, 'year')
      return { start: y.startOf('year'), end: y.endOf('year') }
    }
    case 'nextYear': {
      const y = ref.add(1, 'year')
      return { start: y.startOf('year'), end: y.endOf('year') }
    }
    case 'yearToDate':
      return { start: ref.startOf('year'), end: ref }
  }
}

/**
 * Builds a concrete {@link DateFilterModel} for a relative period, resolved
 * against `reference` (defaults to now). Single-day periods emit an `equals`
 * condition; multi-day periods emit an `inRange` over the inclusive [start, end]
 * days. All values are `YYYY-MM-DD` day keys, matching date-column granularity.
 */
export const buildRelativeDateFilter = (
  period: RelativeDatePeriod,
  reference: dayjs.Dayjs = dayjs(),
): DateFilterModel => {
  const ref = reference.startOf('day')
  const { start, end } = rangeFor(period, ref)
  const startKey = start.format(YMD)
  const endKey = end.format(YMD)

  const condition: DateCondition =
    startKey === endKey
      ? { op: 'equals', value: startKey }
      : { op: 'inRange', value: startKey, valueTo: endKey }

  return { type: 'date', conditions: [condition], join: 'AND' }
}
