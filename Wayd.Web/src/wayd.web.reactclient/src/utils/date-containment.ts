import { Dayjs } from 'dayjs'

/**
 * The rule a parent's dates follow when it has dated children, shared by the
 * roadmap and the project plan: moving the whole range (a shift) carries the
 * children along, so it is always allowed; changing its length (a resize)
 * must still contain every child. The domain enforces the same rule.
 */

/** The collective range of a parent's direct children. */
export interface ChildrenSpan {
  start: Dayjs
  end: Dayjs
  /** The name of the child that starts first, for messages. */
  earliest: string
  /** The name of the child that ends last, for messages. */
  latest: string
}

/**
 * The span of the given children, or null when none has dates. `rangeOf`
 * gives a child's [start, end] — a milestone's single date as both ends.
 */
export const childrenSpan = <C>(
  children: readonly C[] | undefined,
  rangeOf: (child: C) => [Dayjs, Dayjs] | null,
  nameOf: (child: C) => string | undefined,
): ChildrenSpan | null => {
  let span: ChildrenSpan | null = null
  for (const child of children ?? []) {
    const range = rangeOf(child)
    if (!range) continue
    const [start, end] = range
    const name = nameOf(child) ?? ''
    if (!span) {
      span = { start, end, earliest: name, latest: name }
      continue
    }
    if (start.isBefore(span.start, 'day')) {
      span.start = start
      span.earliest = name
    }
    if (end.isAfter(span.end, 'day')) {
      span.end = end
      span.latest = name
    }
  }
  return span
}

/**
 * Whether a date change is a pure shift: both ends move by the same number of
 * days, and by at least one.
 */
export const isShiftOnlyChange = (
  originalStart: Dayjs | null | undefined,
  originalEnd: Dayjs | null | undefined,
  newStart: Dayjs | null | undefined,
  newEnd: Dayjs | null | undefined,
): boolean => {
  if (!originalStart || !originalEnd || !newStart || !newEnd) return false
  const startDelta = newStart.diff(originalStart, 'day')
  const endDelta = newEnd.diff(originalEnd, 'day')
  return startDelta === endDelta && startDelta !== 0
}

/**
 * The message when a parent's proposed range would not contain its
 * children, or null when it does or the inputs are incomplete.
 */
export const getChildrenContainmentError = (
  span: ChildrenSpan | null,
  start: Dayjs | null | undefined,
  end: Dayjs | null | undefined,
): string | null => {
  if (!span || !start || !end) return null

  if (start.isAfter(span.start, 'day')) {
    return `Start date must be on or before child item “${span.earliest}” (${span.start.format('MMM D, YYYY')}).`
  }
  if (end.isBefore(span.end, 'day')) {
    return `End date must be on or after child item “${span.latest}” (${span.end.format('MMM D, YYYY')}).`
  }
  return null
}

/**
 * Applies the shift-or-resize rule to a parent's date change: null for a
 * shift, or for a resize that still contains the children; otherwise the
 * end that cuts a child off and why.
 */
export const rangeChangeContainmentError = ({
  original,
  next,
  span,
}: {
  original: [Dayjs | null | undefined, Dayjs | null | undefined]
  next: [Dayjs, Dayjs]
  span: ChildrenSpan | null
}): { role: 'start' | 'end'; message: string } | null => {
  if (!span) return null
  const [start, end] = next
  if (isShiftOnlyChange(original[0], original[1], start, end)) return null
  const message = getChildrenContainmentError(span, start, end)
  if (!message) return null
  return { role: start.isAfter(span.start, 'day') ? 'start' : 'end', message }
}
