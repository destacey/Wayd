import type { FilterFn } from '@tanstack/react-table'

/**
 * Case-insensitive substring match filter.
 * Mirrors TanStack's built-in `includesString` behavior as a reusable function.
 * Caches the normalized filter value for performance across row iterations.
 */
export const stringContainsFilter: FilterFn<any> = (() => {
  let lastFilterValue: unknown = undefined
  let lastNeedle = ''

  return (row, columnId, filterValue, _addMeta) => {
    if (filterValue !== lastFilterValue) {
      lastFilterValue = filterValue
      lastNeedle = String(filterValue ?? '')
        .trim()
        .toLowerCase()
    }

    const needle = lastNeedle
    if (!needle) return true

    const value = row.getValue(columnId)
    return String(value ?? '')
      .toLowerCase()
      .includes(needle)
  }
})()

/**
 * Multi-select filter where the filter value is either an array of values (a "set") or a single value.
 * Caches parsed filter state for performance.
 */
export const setContainsFilter: FilterFn<any> = (() => {
  let lastFilterValue: unknown = undefined
  let lastMode: 'none' | 'single' | 'set' = 'none'
  let lastSingle: unknown = null
  let lastSet: Set<unknown> | null = null

  return (row, columnId, filterValue, _addMeta) => {
    if (filterValue !== lastFilterValue) {
      lastFilterValue = filterValue

      if (filterValue == null) {
        lastMode = 'none'
        lastSingle = null
        lastSet = null
      } else if (Array.isArray(filterValue)) {
        if (filterValue.length === 0) {
          lastMode = 'none'
          lastSingle = null
          lastSet = null
        } else {
          lastMode = 'set'
          lastSingle = null
          lastSet = new Set(filterValue as unknown[])
        }
      } else {
        lastMode = 'single'
        lastSingle = filterValue
        lastSet = null
      }
    }

    if (lastMode === 'none') return true

    const value = row.getValue(columnId)
    if (value == null) return false

    if (lastMode === 'set') {
      return lastSet?.has(value) ?? false
    }

    return lastSingle === value
  }
})()

type ParsedNumberRangeFilter =
  | { kind: 'none' }
  | { kind: 'invalid' }
  | { kind: 'eq'; value: number }
  | { kind: 'cmp'; op: '<' | '<=' | '>' | '>='; value: number }
  | { kind: 'between'; min: number | null; max: number | null }

const parseNumberRangeFilter = (rawInput: unknown): ParsedNumberRangeFilter => {
  const input = String(rawInput ?? '').trim()
  if (!input) return { kind: 'none' }

  // Comparators: >=4, <= 2.5, >0, < 10
  const cmpMatch = /^(<=|>=|<|>)\s*(-?\d+(?:\.\d+)?)$/.exec(input)
  if (cmpMatch) {
    const op = cmpMatch[1] as '<' | '<=' | '>' | '>='
    const value = Number(cmpMatch[2])
    if (Number.isNaN(value)) return { kind: 'invalid' }
    return { kind: 'cmp', op, value }
  }

  // Ranges:
  // - 2-6 or 2..6
  // - ..6
  // - 2..
  const openUpper = /^\.\.\s*(-?\d+(?:\.\d+)?)$/.exec(input)
  if (openUpper) {
    const max = Number(openUpper[1])
    if (Number.isNaN(max)) return { kind: 'invalid' }
    return { kind: 'between', min: null, max }
  }

  const openLower = /^(-?\d+(?:\.\d+)?)\s*\.\.$/.exec(input)
  if (openLower) {
    const min = Number(openLower[1])
    if (Number.isNaN(min)) return { kind: 'invalid' }
    return { kind: 'between', min, max: null }
  }

  const rangeMatch =
    /^(-?\d+(?:\.\d+)?)\s*(?:-|\.\.)\s*(-?\d+(?:\.\d+)?)$/.exec(input)
  if (rangeMatch) {
    const a = Number(rangeMatch[1])
    const b = Number(rangeMatch[2])
    if (Number.isNaN(a) || Number.isNaN(b)) return { kind: 'invalid' }
    const min = Math.min(a, b)
    const max = Math.max(a, b)
    return { kind: 'between', min, max }
  }

  // Exact numeric (fallback)
  const value = Number(input)
  if (Number.isNaN(value)) return { kind: 'invalid' }
  return { kind: 'eq', value }
}

/**
 * Numeric column filter supporting:
 * - exact: 4
 * - comparisons: >=4, < 10
 * - ranges: 2-6, 2..6, ..6, 2..
 * Blank or invalid input passes all rows.
 */
export const numberRangeFilter: FilterFn<any> = (() => {
  const cache = new Map<
    string,
    { lastFilterValue: unknown; parsed: ParsedNumberRangeFilter }
  >()

  return (row, columnId, filterValue, _addMeta) => {
    const cached = cache.get(columnId)
    const parsed =
      cached && cached.lastFilterValue === filterValue
        ? cached.parsed
        : (() => {
            const next = parseNumberRangeFilter(filterValue)
            cache.set(columnId, { lastFilterValue: filterValue, parsed: next })
            return next
          })()

    if (parsed.kind === 'none' || parsed.kind === 'invalid') return true

    const rawCellValue = row.getValue(columnId)
    if (rawCellValue == null) return false

    const cellValue =
      typeof rawCellValue === 'number'
        ? rawCellValue
        : (() => {
            const s = String(rawCellValue).trim()
            if (!s) return Number.NaN
            return Number(s)
          })()

    if (Number.isNaN(cellValue)) return false

    switch (parsed.kind) {
      case 'eq':
        return cellValue === parsed.value
      case 'cmp':
        if (parsed.op === '>') return cellValue > parsed.value
        if (parsed.op === '>=') return cellValue >= parsed.value
        if (parsed.op === '<') return cellValue < parsed.value
        return cellValue <= parsed.value
      case 'between':
        return (
          (parsed.min == null || cellValue >= parsed.min) &&
          (parsed.max == null || cellValue <= parsed.max)
        )
      default:
        return true
    }
  }
})()
