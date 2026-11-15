import dayjs from 'dayjs'
import type { ColumnFiltersState, SortingState } from '@tanstack/react-table'

import {
  SET_FILTER_BLANK,
  isConditionActive,
  operatorNeedsValue,
  type ColumnFilterModel,
  type DateCondition,
  type DateTimeCondition,
  type NumberCondition,
  type TextCondition,
} from '../filters/filter-model'

/**
 * Translates the grid's filter and sort state into the `filter` and `sort`
 * parameters of a server-paged list, in Gridify's syntax (see "Filtering and
 * sorting a paged list" in docs/contributing/api.mdx). Each column's id is the
 * field name the server maps.
 *
 * The translation keeps client mode's meaning: text compares ignoring case
 * (the database collation does), a number range is inclusive whichever way
 * round it was entered, a date column compares calendar dates
 * (`YYYY-MM-DD`, a LocalDate field) and a date-time column compares to the
 * minute in the viewer's time zone (an ISO instant, an Instant field).
 */

/**
 * Escapes a value so Gridify reads it as text. Gridify reads `\x` as `x` for
 * any character, so escaping everything but letters, digits and spaces is
 * always safe — and it covers the separators (`, | ( )`), an operator at the
 * start of a value (`*`, `=`, `^`, `<`), and the `/i` suffix alike.
 */
export const escapeGridifyValue = (value: string): string =>
  value.replace(/[^\p{L}\p{N} ]/gu, (c) => `\\${c}`)

const group = (parts: string[], join: ',' | '|'): string =>
  parts.length === 1 ? parts[0] : `(${parts.join(join)})`

const textCondition = (field: string, c: TextCondition): string | null => {
  if (c.op === 'blank') return `${field}=`
  if (c.op === 'notBlank') return `${field}!=`
  const value = c.value.trim()
  if (value === '') return null
  const v = escapeGridifyValue(value)
  switch (c.op) {
    case 'contains':
      return `${field}=*${v}`
    case 'notContains':
      return `${field}!*${v}`
    case 'equals':
      return `${field}=${v}`
    case 'notEqual':
      return `${field}!=${v}`
    case 'startsWith':
      return `${field}^${v}`
    case 'endsWith':
      return `${field}$${v}`
  }
}

const numberCondition = (field: string, c: NumberCondition): string | null => {
  if (c.op === 'blank') return `${field}=`
  if (c.op === 'notBlank') return `${field}!=`
  const a = c.value
  if (a === null || a === undefined) return null
  switch (c.op) {
    case 'equals':
      return `${field}=${a}`
    case 'notEqual':
      return `${field}!=${a}`
    case 'greaterThan':
      return `${field}>${a}`
    case 'greaterThanOrEqual':
      return `${field}>=${a}`
    case 'lessThan':
      return `${field}<${a}`
    case 'lessThanOrEqual':
      return `${field}<=${a}`
    case 'inRange': {
      const b = c.valueTo
      if (b === null || b === undefined) return null
      return `(${field}>=${Math.min(a, b)},${field}<=${Math.max(a, b)})`
    }
  }
}

/** A calendar date (`YYYY-MM-DD`), as a LocalDate field reads it. */
const dateCondition = (field: string, c: DateCondition): string | null => {
  if (c.op === 'blank') return `${field}=`
  if (c.op === 'notBlank') return `${field}!=`
  const a = c.value
  if (!a) return null
  switch (c.op) {
    case 'equals':
      return `${field}=${a}`
    case 'notEqual':
      return `${field}!=${a}`
    case 'before':
      return `${field}<${a}`
    case 'after':
      return `${field}>${a}`
    case 'inRange': {
      const b = c.valueTo
      if (!b) return null
      const [min, max] = a <= b ? [a, b] : [b, a]
      return `(${field}>=${min},${field}<=${max})`
    }
  }
}

/** The ISO instant a minute starts at, in the viewer's time zone. */
const minuteStart = (value: string): dayjs.Dayjs | null => {
  const d = dayjs(value)
  return d.isValid() ? d.startOf('minute') : null
}

const iso = (d: dayjs.Dayjs): string => d.toISOString()

/**
 * A moment compared to the minute, as client mode does: "equals 09:30" is the
 * minute from 09:30:00 up to 09:31:00, sent as ISO instants to an Instant field.
 */
const dateTimeCondition = (
  field: string,
  c: DateTimeCondition,
): string | null => {
  if (c.op === 'blank') return `${field}=`
  if (c.op === 'notBlank') return `${field}!=`
  const a = c.value ? minuteStart(c.value) : null
  if (!a) return null
  const next = (d: dayjs.Dayjs) => iso(d.add(1, 'minute'))
  switch (c.op) {
    case 'equals':
      return `(${field}>=${iso(a)},${field}<${next(a)})`
    case 'notEqual':
      return `(${field}<${iso(a)}|${field}>=${next(a)})`
    case 'before':
      return `${field}<${iso(a)}`
    case 'after':
      return `${field}>=${next(a)}`
    case 'inRange': {
      const b = c.valueTo ? minuteStart(c.valueTo) : null
      if (!b) return null
      const [min, max] = a.isBefore(b) ? [a, b] : [b, a]
      return `(${field}>=${iso(min)},${field}<${next(max)})`
    }
  }
}

/** One column's filter as a Gridify expression, or null when it filters nothing. */
export const toGridifyColumnFilter = (
  field: string,
  model: ColumnFilterModel,
): string | null => {
  if (model.type === 'set') {
    if (model.values.length === 0) return null
    const parts = model.values.map((value) =>
      value === SET_FILTER_BLANK
        ? `${field}=`
        : `${field}=${escapeGridifyValue(value)}`,
    )
    return group(parts, '|')
  }

  // The date tree reads the days present in the loaded rows, which server
  // mode does not have; WaydGrid turns it off there.
  if (model.type === 'dateSet') return null

  const active = model.conditions.filter(
    (c) => !operatorNeedsValue(c.op) || isConditionActive(c),
  )
  const parts = active
    .map((c) => {
      switch (model.type) {
        case 'text':
          return textCondition(field, c as TextCondition)
        case 'number':
          return numberCondition(field, c as NumberCondition)
        case 'date':
          return dateCondition(field, c as DateCondition)
        case 'dateTime':
          return dateTimeCondition(field, c as DateTimeCondition)
      }
    })
    .filter((part): part is string => part !== null)

  if (parts.length === 0) return null
  return group(parts, model.join === 'AND' ? ',' : '|')
}

/**
 * The `filter` parameter for the grid's column filters: one expression per
 * column, joined with AND. Undefined when nothing filters.
 */
export const toGridifyFilter = (
  columnFilters: ColumnFiltersState,
): string | undefined => {
  const parts = columnFilters
    .map(({ id, value }) =>
      value && typeof value === 'object'
        ? toGridifyColumnFilter(id, value as ColumnFilterModel)
        : null,
    )
    .filter((part): part is string => part !== null)

  return parts.length > 0 ? parts.join(',') : undefined
}

/** The `sort` parameter for the grid's sort: `status asc, due desc`. */
export const toGridifySort = (sorting: SortingState): string | undefined =>
  sorting.length > 0
    ? sorting.map((s) => `${s.id} ${s.desc ? 'desc' : 'asc'}`).join(', ')
    : undefined
