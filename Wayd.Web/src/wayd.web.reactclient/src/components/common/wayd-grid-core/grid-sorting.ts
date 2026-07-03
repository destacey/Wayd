import type { Row, SortingFn } from '@tanstack/react-table'
import dayjs from 'dayjs'

const compareNumbers = (a: number, b: number): number => {
  return a === b ? 0 : a > b ? 1 : -1
}

type DateSortOptions = {
  emptyValue?: number
}

/**
 * Custom sorting factory that sorts by a derived date value.
 * Handles null/undefined values using a configurable emptyValue (default: -Infinity).
 */
export function dateSortBy(
  getDate: (row: any) => string | number | Date | null | undefined,
  options?: DateSortOptions,
): SortingFn<any>

export function dateSortBy(
  getDate: (row: any) => string | number | Date | null | undefined,
  options: DateSortOptions = {},
): SortingFn<any> {
  const emptyValue = options.emptyValue ?? -Infinity

  return (a, b) => {
    const av = getDate(a)
    const bv = getDate(b)

    const aNum = av ? dayjs(av).valueOf() : emptyValue
    const bNum = bv ? dayjs(bv).valueOf() : emptyValue

    return compareNumbers(aNum, bNum)
  }
}

/** True for values that should sort as "empty": null, undefined, or ''. */
const isEmpty = (v: unknown): boolean => v === null || v === undefined || v === ''

/**
 * Compares two non-empty values. Numbers (and numeric strings) compare
 * numerically; everything else compares as a locale-aware, case-insensitive
 * string — matching TanStack's `auto` behavior closely enough for a default.
 */
const compareNonEmpty = (a: unknown, b: unknown): number => {
  const an = typeof a === 'number' ? a : Number(a)
  const bn = typeof b === 'number' ? b : Number(b)
  const bothNumeric =
    !Number.isNaN(an) &&
    !Number.isNaN(bn) &&
    a !== '' &&
    b !== '' &&
    a !== true &&
    b !== true &&
    a !== false &&
    b !== false
  if (bothNumeric) return an === bn ? 0 : an > bn ? 1 : -1

  return String(a).localeCompare(String(b), undefined, {
    sensitivity: 'base',
    numeric: true,
  })
}

/**
 * Default WaydGrid2 sort that keeps **empty** values (null/undefined/'') at the
 * *end* of an ascending sort — and thus the start of a descending sort, since
 * TanStack negates the result on `desc`. This treats "empty" as the largest
 * value (like `+Infinity`), matching AG Grid / Excel, and fixes TanStack's
 * built-in behavior where a `null` number sorts as `0` (top of ascending).
 *
 * TanStack's own `sortUndefined` only catches strict `undefined`, so API data
 * that uses `null` for empties slips through — this default covers both. Wired
 * as `defaultColumn.sortingFn`; a column can still override with its own
 * `sortingFn` when it needs bespoke ordering.
 */
export const sortEmptyLast: SortingFn<any> = (
  rowA: Row<any>,
  rowB: Row<any>,
  columnId: string,
): number => {
  const a = rowA.getValue(columnId)
  const b = rowB.getValue(columnId)

  const aEmpty = isEmpty(a)
  const bEmpty = isEmpty(b)
  if (aEmpty || bEmpty) {
    if (aEmpty && bEmpty) return 0
    // Treat empty as the largest value → it lands last in ascending. TanStack
    // negates a sortingFn's result on `desc`, so this same +1 flips to place
    // empties first when descending — i.e. "last in asc, first in desc".
    return aEmpty ? 1 : -1
  }

  return compareNonEmpty(a, b)
}

/**
 * Sorts work-item keys of the form `PREFIX-NUMBER` (e.g. `WEB-42`) by prefix
 * first (alphabetically) then by the numeric suffix — so `WEB-9` sorts before
 * `WEB-10`, unlike a plain string sort. Empty keys sort to the end (ascending).
 * A TanStack `sortingFn` mirroring the old AG Grid `workItemKeyComparator`.
 */
export const workItemKeySort: SortingFn<any> = (
  rowA: Row<any>,
  rowB: Row<any>,
  columnId: string,
): number => {
  const a = rowA.getValue(columnId) as string | null | undefined
  const b = rowB.getValue(columnId) as string | null | undefined

  if (!a && !b) return 0
  if (!a) return 1 // empty keys sort to the end
  if (!b) return -1

  const [prefixA, numA] = a.split('-')
  const [prefixB, numB] = b.split('-')

  if (prefixA < prefixB) return -1
  if (prefixA > prefixB) return 1

  return parseInt(numA) - parseInt(numB)
}

/** Work status categories in workflow order; drives {@link workStatusCategorySort}. */
const WORK_STATUS_CATEGORY_ORDER = ['Proposed', 'Active', 'Done', 'Removed']

/**
 * Sorts a work status category column by its position in the workflow
 * (Proposed → Active → Done → Removed) rather than alphabetically. A TanStack
 * `sortingFn` mirroring the old AG Grid `workStatusCategoryComparator`.
 */
export const workStatusCategorySort: SortingFn<any> = (
  rowA: Row<any>,
  rowB: Row<any>,
  columnId: string,
): number => {
  const a = rowA.getValue(columnId) as string
  const b = rowB.getValue(columnId) as string
  return (
    WORK_STATUS_CATEGORY_ORDER.indexOf(a) -
    WORK_STATUS_CATEGORY_ORDER.indexOf(b)
  )
}
