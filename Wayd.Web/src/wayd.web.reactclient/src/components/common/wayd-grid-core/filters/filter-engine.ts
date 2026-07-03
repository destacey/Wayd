/**
 * WaydGrid2 filter engine.
 *
 * Pure predicate functions that evaluate a {@link ColumnFilterModel} descriptor
 * against a cell value, plus a single TanStack `FilterFn` that dispatches on the
 * descriptor's `type`. UI-free and unit-testable.
 */
import type { FilterFn } from '@tanstack/react-table'
import dayjs from 'dayjs'

import {
  isConditionActive,
  operatorNeedsValue,
  type ColumnFilterModel,
  type DateCondition,
  type DateTimeCondition,
  type FilterJoin,
  type NumberCondition,
  type TextCondition,
} from './filter-model'

/** Granularity used when comparing values in date vs date-time filters. */
type DateUnit = 'day' | 'minute'

// ─── Per-condition evaluation ──────────────────────────────

const normalizeText = (value: unknown): string =>
  String(value ?? '')
    .trim()
    .toLowerCase()

const evalTextCondition = (
  condition: TextCondition,
  cellValue: unknown,
): boolean => {
  const cell = normalizeText(cellValue)

  switch (condition.op) {
    case 'blank':
      return cell === ''
    case 'notBlank':
      return cell !== ''
  }

  const needle = normalizeText(condition.value)
  // Value operators with no needle yet are treated as "pass" so a
  // half-typed condition doesn't hide everything (matched by isConditionActive
  // at the descriptor level, but guarded here too for safety).
  if (needle === '') return true

  switch (condition.op) {
    case 'contains':
      return cell.includes(needle)
    case 'notContains':
      return !cell.includes(needle)
    case 'equals':
      return cell === needle
    case 'notEqual':
      return cell !== needle
    case 'startsWith':
      return cell.startsWith(needle)
    case 'endsWith':
      return cell.endsWith(needle)
    default:
      return true
  }
}

const toNumber = (value: unknown): number => {
  if (typeof value === 'number') return value
  const s = String(value ?? '').trim()
  if (!s) return Number.NaN
  return Number(s)
}

const evalNumberCondition = (
  condition: NumberCondition,
  cellValue: unknown,
): boolean => {
  if (condition.op === 'blank') return cellValue == null || cellValue === ''
  if (condition.op === 'notBlank') return cellValue != null && cellValue !== ''

  const cell = toNumber(cellValue)
  if (Number.isNaN(cell)) return false

  const a = condition.value
  if (a === null || a === undefined) return true

  switch (condition.op) {
    case 'equals':
      return cell === a
    case 'notEqual':
      return cell !== a
    case 'greaterThan':
      return cell > a
    case 'greaterThanOrEqual':
      return cell >= a
    case 'lessThan':
      return cell < a
    case 'lessThanOrEqual':
      return cell <= a
    case 'inRange': {
      const b = condition.valueTo
      if (b === null || b === undefined) return true
      const min = Math.min(a, b)
      const max = Math.max(a, b)
      return cell >= min && cell <= max
    }
    default:
      return true
  }
}

const toMoment = (value: unknown, unit: DateUnit): dayjs.Dayjs | null => {
  if (value == null || value === '') return null
  const d = dayjs(value as string | number | Date)
  return d.isValid() ? d.startOf(unit) : null
}

/**
 * Normalizes a cell value to its `YYYY-MM-DD` day key, or null when it isn't a
 * valid date. Used by the date-set (date tree) filter and to build the tree's
 * distinct-day options from column data, so a cell matches regardless of its
 * raw shape (Date, ISO timestamp, plain date string).
 */
export const toDayKey = (value: unknown): string | null => {
  const d = toMoment(value, 'day')
  return d ? d.format('YYYY-MM-DD') : null
}

/**
 * Shared evaluator for `date` (day granularity) and `dateTime` (minute
 * granularity) conditions. The `unit` controls the comparison precision.
 */
const evalTemporalCondition = (
  condition: DateCondition | DateTimeCondition,
  cellValue: unknown,
  unit: DateUnit,
): boolean => {
  if (condition.op === 'blank') return cellValue == null || cellValue === ''
  if (condition.op === 'notBlank') return cellValue != null && cellValue !== ''

  const cell = toMoment(cellValue, unit)
  if (!cell) return false

  const a = toMoment(condition.value, unit)
  if (!a) return true

  switch (condition.op) {
    case 'equals':
      return cell.isSame(a, unit)
    case 'notEqual':
      return !cell.isSame(a, unit)
    case 'before':
      return cell.isBefore(a, unit)
    case 'after':
      return cell.isAfter(a, unit)
    case 'inRange': {
      const b = toMoment(condition.valueTo, unit)
      if (!b) return true
      const min = a.isBefore(b) ? a : b
      const max = a.isBefore(b) ? b : a
      return (
        (cell.isSame(min, unit) || cell.isAfter(min, unit)) &&
        (cell.isSame(max, unit) || cell.isBefore(max, unit))
      )
    }
    default:
      return true
  }
}

// ─── Join reducer ──────────────────────────────────────────

const combine = (
  results: boolean[],
  join: FilterJoin,
): boolean => {
  if (results.length === 0) return true
  return join === 'AND'
    ? results.every(Boolean)
    : results.some(Boolean)
}

// ─── Descriptor evaluation ─────────────────────────────────

/**
 * Evaluate a full descriptor against a cell value. Returns true (pass) when the
 * descriptor is empty, so an unfiltered column never hides rows.
 */
export const evaluateFilterModel = (
  model: ColumnFilterModel,
  cellValue: unknown,
): boolean => {
  if (model.type === 'set') {
    if (model.values.length === 0) return true
    if (cellValue == null) return false
    return model.values.includes(String(cellValue))
  }

  if (model.type === 'dateSet') {
    if (model.values.length === 0) return true
    const key = toDayKey(cellValue)
    if (key === null) return false
    return model.values.includes(key)
  }

  const activeConditions = model.conditions.filter(
    (c) => operatorNeedsValue(c.op) === false || isConditionActive(c),
  )
  if (activeConditions.length === 0) return true

  const results = activeConditions.map((condition) => {
    switch (model.type) {
      case 'text':
        return evalTextCondition(condition as TextCondition, cellValue)
      case 'number':
        return evalNumberCondition(condition as NumberCondition, cellValue)
      case 'date':
        return evalTemporalCondition(
          condition as DateCondition,
          cellValue,
          'day',
        )
      case 'dateTime':
        return evalTemporalCondition(
          condition as DateTimeCondition,
          cellValue,
          'minute',
        )
      default:
        return true
    }
  })

  return combine(results, model.join)
}

// ─── TanStack FilterFn ─────────────────────────────────────

/**
 * Single TanStack filter function for all descriptor-based column filters.
 * The column's filter value is a {@link ColumnFilterModel}; this dispatches on
 * its `type`. Assign to a column via `filterFn: waydColumnFilter`.
 */
export const waydColumnFilter: FilterFn<any> = (
  row,
  columnId,
  filterValue,
) => {
  if (!filterValue || typeof filterValue !== 'object') return true
  const model = filterValue as ColumnFilterModel
  return evaluateFilterModel(model, row.getValue(columnId))
}
