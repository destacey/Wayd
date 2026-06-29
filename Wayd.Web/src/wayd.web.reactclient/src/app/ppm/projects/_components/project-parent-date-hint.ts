import dayjs, { Dayjs } from 'dayjs'

interface ParentRange {
  name: string
  start: Dayjs
  end: Dayjs
}

/**
 * Finds a parent node (phase or task) anywhere in the plan tree and returns its name and range.
 */
export const findParentPlanNodeRange = (
  nodes: any[] | undefined,
  parentId: string | undefined,
): ParentRange | null => {
  if (!nodes || !parentId) return null

  const stack: any[] = [...nodes]
  while (stack.length > 0) {
    const node = stack.pop()!
    if (node.id.toLowerCase() === parentId.toLowerCase()) {
      if (!node.start) return null
      return {
        name: node.name ?? 'parent',
        start: dayjs(node.start),
        end: node.end ? dayjs(node.end) : dayjs(node.start),
      }
    }
    const children = node.children
    if (children?.length) {
      stack.push(...children)
    }
  }

  return null
}

/**
 * Builds the hint for a ranged child task. Returns null when child is inside the parent or inputs are incomplete.
 */
export const getParentExpansionHint = (
  parent: ParentRange | null,
  childStart: Dayjs | null | undefined,
  childEnd: Dayjs | null | undefined,
): string | null => {
  if (!parent || !childStart) return null

  const extendsBefore = childStart.isBefore(parent.start, 'day')
  const extendsAfter = childEnd && childEnd.isAfter(parent.end, 'day')
  if (!extendsBefore && !extendsAfter) return null

  return `These dates fall outside the parent “${parent.name}”, which will expand to include them.`
}

/**
 * Builds the hint for a milestone child. Returns null when inside the parent or inputs are incomplete.
 */
export const getMilestoneParentExpansionHint = (
  parent: ParentRange | null,
  date: Dayjs | null | undefined,
): string | null => {
  if (!parent || !date) return null

  const outside = date.isBefore(parent.start, 'day') || date.isAfter(parent.end, 'day')
  if (!outside) return null

  return `This date falls outside the parent “${parent.name}”, which will expand to include it.`
}

export interface ChildrenSpan {
  start: Dayjs
  end: Dayjs
  earliest: string
  latest: string
}

const childItemRange = (child: any): [Dayjs, Dayjs] | null => {
  if (child.plannedDate) {
    const d = dayjs(child.plannedDate)
    return [d, d]
  }
  if (child.start && child.end) {
    return [dayjs(child.start), dayjs(child.end)]
  }
  if (child.start) {
    const d = dayjs(child.start)
    return [d, d]
  }
  return null
}

/**
 * Finds the collective span of direct child items of a given parent node.
 */
export const findOwnChildrenSpan = (
  nodes: any[] | undefined,
  itemId: string | undefined,
): ChildrenSpan | null => {
  if (!nodes || !itemId) return null

  const stack: any[] = [...nodes]
  while (stack.length > 0) {
    const node = stack.pop()!
    if (node.id.toLowerCase() === itemId.toLowerCase()) {
      const children = node.children ?? []
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
    const children = node.children
    if (children?.length) {
      stack.push(...children)
    }
  }

  return null
}

/**
 * Validates that a parent's proposed dates contain all child items.
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
