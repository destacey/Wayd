import dayjs, { Dayjs } from 'dayjs'
import type {
  RoadmapActivityListDto,
  RoadmapItemListDto,
} from '@/src/services/wayd-api'

/**
 * Helpers for the non-blocking "this will expand the parent" hints shown on the
 * roadmap item forms.
 *
 * These are intentionally informational only: the domain auto-expands a parent
 * activity to contain its children (date rollup), so a child falling outside its
 * parent is valid — we just set the user's expectation that the parent will grow.
 */

interface ParentRange {
  name: string
  start: Dayjs
  end: Dayjs
}

/**
 * Finds an activity anywhere in the (possibly nested) activities tree and returns
 * its name and date range, or null when not found / no parent selected.
 */
export const findParentActivityRange = (
  activities: RoadmapActivityListDto[] | undefined,
  parentId: string | undefined,
): ParentRange | null => {
  if (!activities || !parentId) return null

  const stack: RoadmapActivityListDto[] = [...activities]
  while (stack.length > 0) {
    const node = stack.pop()!
    if (node.id === parentId) {
      return {
        name: node.name ?? 'parent activity',
        start: dayjs(node.start),
        end: dayjs(node.end),
      }
    }
    const children = node.children as RoadmapActivityListDto[] | undefined
    if (children?.length) {
      stack.push(...children)
    }
  }

  return null
}

/**
 * Builds the hint for a ranged child (Activity / Timebox). Returns null when the
 * child range is fully inside the parent (no expansion) or inputs are incomplete.
 */
export const getParentExpansionHint = (
  parent: ParentRange | null,
  childStart: Dayjs | null | undefined,
  childEnd: Dayjs | null | undefined,
): string | null => {
  if (!parent || !childStart || !childEnd) return null

  const extendsBefore = childStart.isBefore(parent.start, 'day')
  const extendsAfter = childEnd.isAfter(parent.end, 'day')
  if (!extendsBefore && !extendsAfter) return null

  return `These dates fall outside the parent activity “${parent.name}”, which will expand to include them.`
}

/**
 * Builds the hint for a milestone child whose single date falls outside the
 * parent activity. Returns null when inside the parent or inputs are incomplete.
 */
export const getMilestoneParentExpansionHint = (
  parent: ParentRange | null,
  date: Dayjs | null | undefined,
): string | null => {
  if (!parent || !date) return null

  const outside = date.isBefore(parent.start, 'day') || date.isAfter(parent.end, 'day')
  if (!outside) return null

  return `This date falls outside the parent activity “${parent.name}”, which will expand to include it.`
}

interface ChildrenSpan {
  start: Dayjs
  end: Dayjs
  /** The child whose start/end defines the relevant boundary, for messaging. */
  earliest: string
  latest: string
}

/** The effective [start, end] of a child item: a range for Activity/Timebox, the date for a Milestone. */
const childItemRange = (child: RoadmapItemListDto): [Dayjs, Dayjs] | null => {
  const anyChild = child as RoadmapItemListDto & {
    start?: Date
    end?: Date
    date?: Date
  }
  if (anyChild.date) {
    const d = dayjs(anyChild.date)
    return [d, d]
  }
  if (anyChild.start && anyChild.end) {
    return [dayjs(anyChild.start), dayjs(anyChild.end)]
  }
  return null
}

/**
 * Finds an activity by id in the (possibly nested) tree and returns the collective
 * span of its direct children, or null when not found / it has no dated children.
 */
export const findOwnChildrenSpan = (
  activities: RoadmapActivityListDto[] | undefined,
  itemId: string | undefined,
): ChildrenSpan | null => {
  if (!activities || !itemId) return null

  const stack: RoadmapActivityListDto[] = [...activities]
  while (stack.length > 0) {
    const node = stack.pop()!
    if (node.id === itemId) {
      const children = (node.children ?? []) as RoadmapItemListDto[]
      let span: ChildrenSpan | null = null
      for (const child of children) {
        const range = childItemRange(child)
        if (!range) continue
        const [start, end] = range
        if (!span) {
          span = { start, end, earliest: child.name ?? '', latest: child.name ?? '' }
        } else {
          if (start.isBefore(span.start, 'day')) {
            span.start = start
            span.earliest = child.name ?? ''
          }
          if (end.isAfter(span.end, 'day')) {
            span.end = end
            span.latest = child.name ?? ''
          }
        }
      }
      return span
    }
    const childActivities = node.children as RoadmapActivityListDto[] | undefined
    if (childActivities?.length) {
      stack.push(...childActivities)
    }
  }

  return null
}

/**
 * Blocking validation for the item being edited: an Activity's range must contain
 * all of its own children. Returns an error message when the proposed range would
 * fall inside the children, or null when valid / not applicable.
 *
 * This mirrors the domain rule (a parent cannot be shrunk behind its children) so
 * the user gets feedback before the API call.
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
 * Checks if a date change is a pure shift (duration is identical, but shifted in time).
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
