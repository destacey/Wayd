import dayjs, { Dayjs } from 'dayjs'
import { childrenSpan, type ChildrenSpan } from '@/src/utils/date-containment'

interface ParentRange {
  name: string
  start: Dayjs
  end: Dayjs
}

/**
 * Finds a parent node (stage or task) anywhere in the plan tree and returns its name and range.
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

/** The collective span of a plan node's direct children, or null when none
 *  has dates. A milestone counts as its single date. */
export const planChildrenSpan = (
  children: readonly any[] | undefined,
): ChildrenSpan | null =>
  childrenSpan(children ?? [], childItemRange, (child: any) => child.name)

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
      return planChildrenSpan(node.children)
    }
    const children = node.children
    if (children?.length) {
      stack.push(...children)
    }
  }

  return null
}
