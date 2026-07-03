/**
 * Human-readable summaries of a {@link ColumnFilterModel}, for the floating
 * filter row. When the active filter is more complex than the floating widget
 * can faithfully represent (a non-equals date operator, a range, a date-tree
 * selection, multiple conditions), the row shows a read-only summary chip
 * instead of an editable control — so a `before`/`inRange` filter no longer
 * masquerades as a plain equals date. Pure and unit-testable.
 */
import dayjs from 'dayjs'

import { operatorNeedsValue, type ColumnFilterModel } from './filter-model'

/** Short, timezone-safe day label (`YYYY-MM-DD` → `2026-06-21`). */
const dayLabel = (value: string | null | undefined): string => {
  if (!value) return '?'
  const d = dayjs(value)
  return d.isValid() ? d.format('YYYY-MM-DD') : String(value)
}

/**
 * Whether a *date* filter is simple enough for the floating DatePicker to both
 * display and edit without losing information: no filter, or exactly one
 * `equals` condition. Everything else (before/after/inRange/blank, a date-tree
 * `dateSet`, or multiple conditions) must be shown read-only.
 */
export const canFloatingEditDate = (
  model: ColumnFilterModel | undefined,
): boolean => {
  if (!model) return true
  if (model.type !== 'date') return false
  if (model.conditions.length !== 1) return false
  return model.conditions[0].op === 'equals'
}

/**
 * A compact read-only summary of a *date* column's filter, e.g. `< 2026-06-21`,
 * `2026-06-21 – 2026-06-27`, `Blank`, or `Multiple dates`. Returns an empty
 * string when there's nothing to summarize (unfiltered).
 */
export const describeDateFilter = (
  model: ColumnFilterModel | undefined,
): string => {
  if (!model) return ''

  if (model.type === 'dateSet') {
    const n = model.values.length
    if (n === 0) return ''
    if (n === 1) return dayLabel(model.values[0])
    return `${n} dates`
  }

  if (model.type !== 'date') return ''

  const conditions = model.conditions.filter(
    (c) => !operatorNeedsValue(c.op) || c.value != null,
  )
  if (conditions.length === 0) return ''
  if (conditions.length > 1) return 'Multiple conditions'

  const c = conditions[0]
  switch (c.op) {
    case 'equals':
      return dayLabel(c.value)
    case 'notEqual':
      return `≠ ${dayLabel(c.value)}`
    case 'before':
      return `< ${dayLabel(c.value)}`
    case 'after':
      return `> ${dayLabel(c.value)}`
    case 'inRange':
      return `${dayLabel(c.value)} – ${dayLabel(c.valueTo)}`
    case 'blank':
      return 'Blank'
    case 'notBlank':
      return 'Not blank'
    default:
      return dayLabel(c.value)
  }
}
