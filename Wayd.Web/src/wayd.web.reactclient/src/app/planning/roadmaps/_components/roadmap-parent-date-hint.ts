import dayjs, { Dayjs } from 'dayjs'
import type { RoadmapActivityListDto } from '@/src/services/wayd-api'
import { childrenSpan, type ChildrenSpan } from '@/src/utils/date-containment'

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

/** A roadmap item's dates, as both the API's items and the grid's rows carry them. */
interface DatedItem {
  name?: string
  start?: Date | null
  end?: Date | null
  date?: Date | null
}

/** The effective [start, end] of a child item: a range for Activity/Timebox, the date for a Milestone. */
const childItemRange = (child: DatedItem): [Dayjs, Dayjs] | null => {
  if (child.date) {
    const d = dayjs(child.date)
    return [d, d]
  }
  if (child.start && child.end) {
    return [dayjs(child.start), dayjs(child.end)]
  }
  return null
}

/** The collective span of the given items' dates, or null when none has any. */
export const datedItemsSpan = (
  items: readonly DatedItem[] | undefined,
): ChildrenSpan | null =>
  childrenSpan(items, childItemRange, (item) => item.name)

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
      return datedItemsSpan((node.children ?? []) as DatedItem[])
    }
    const childActivities = node.children as RoadmapActivityListDto[] | undefined
    if (childActivities?.length) {
      stack.push(...childActivities)
    }
  }

  return null
}
