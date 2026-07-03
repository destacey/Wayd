/**
 * Groups a flat list of `YYYY-MM-DD` day keys into the Year → Month → Day tree
 * shown in the Excel-style date filter (the checkbox tree). Pure and
 * dayjs-based so it is unit-testable.
 */
import dayjs from 'dayjs'
import customParseFormat from 'dayjs/plugin/customParseFormat'

// Strict parsing (`dayjs(key, fmt, true)`) and `format('MMMM')` from a
// `YYYY-MM` string require this plugin. Extending is idempotent.
dayjs.extend(customParseFormat)

export interface DayNode {
  /** `YYYY-MM-DD` day key. */
  key: string
  /** Day of month, e.g. "17". */
  label: string
}

export interface MonthNode {
  /** `YYYY-MM` key. */
  key: string
  /** Localized month name, e.g. "June". */
  label: string
  days: DayNode[]
}

export interface YearNode {
  /** `YYYY` key. */
  key: string
  label: string
  months: MonthNode[]
}

/**
 * Builds an ascending Year → Month → Day tree from distinct day keys. Invalid
 * keys are ignored. Each level is sorted chronologically.
 */
export const buildDateTree = (dayKeys: string[]): YearNode[] => {
  const years = new Map<string, Map<string, DayNode[]>>()

  for (const key of dayKeys) {
    const d = dayjs(key, 'YYYY-MM-DD', true)
    if (!d.isValid()) continue

    const yearKey = d.format('YYYY')
    const monthKey = d.format('YYYY-MM')

    let months = years.get(yearKey)
    if (!months) {
      months = new Map()
      years.set(yearKey, months)
    }

    let days = months.get(monthKey)
    if (!days) {
      days = []
      months.set(monthKey, days)
    }
    days.push({ key, label: d.format('D') })
  }

  const sortAsc = (a: string, b: string) => (a < b ? -1 : a > b ? 1 : 0)

  return Array.from(years.keys())
    .sort(sortAsc)
    .map((yearKey) => {
      const months = years.get(yearKey)!
      return {
        key: yearKey,
        label: yearKey,
        months: Array.from(months.keys())
          .sort(sortAsc)
          .map((monthKey) => ({
            key: monthKey,
            label: dayjs(monthKey, 'YYYY-MM').format('MMMM'),
            days: months
              .get(monthKey)!
              .slice()
              .sort((a, b) => sortAsc(a.key, b.key)),
          })),
      }
    })
}

/** Flattens a tree back to its day keys, in tree order. */
export const dayKeysOf = (tree: YearNode[]): string[] =>
  tree.flatMap((y) => y.months.flatMap((m) => m.days.map((d) => d.key)))
