'use client'

import type { ColumnDef } from '@tanstack/react-table'

import { createMultiValueSetFilter } from './filters'
import { caseInsensitiveCompare } from './grid-sorting'
import TagListCell from './tag-list-cell'
import type { FilterOption } from './types'

/**
 * Splits a comma-separated string into its trimmed, non-empty values. The
 * default {@link CsvColumnOptions.getValues} for columns whose underlying data
 * is a single CSV string (e.g. `"Alice, Bob"`).
 */
export const splitCsv = (value: string | null | undefined): string[] =>
  (value ?? '')
    .split(',')
    .map((v) => v.trim())
    .filter((v) => v.length > 0)

export interface CsvColumnOptions<T> {
  /** Column id (also the fallback accessor/sort key). */
  id: string
  /** Header text. */
  header: string
  size?: number
  /**
   * The row data the grid will display. Used to derive the set filter's
   * checkbox list from the *individual* values present (not whole CSV combos).
   * Pass the same array you hand the grid.
   */
  data: T[] | undefined
  /**
   * Extracts a row's individual values. Provide this when the source is an
   * array (e.g. `row.tags`) or objects (`row.owners.map(o => o.name)`). Omit it
   * for a plain CSV string column and the factory reads `row[id]` and splits on
   * commas.
   */
  getValues?: (row: T) => string[]
}

/**
 * Builds a fully-wired grid column for a **multi-value ("CSV") column** — one
 * whose underlying data is several discrete values, whether stored as a
 * comma-separated string or an array.
 *
 * It bundles the three pieces such a column always needs, so callers don't
 * hand-roll them per model:
 * - a **tag-list cell** ({@link TagListCell}) rendering each value as a Tag;
 * - a **multi-value set filter** ({@link createMultiValueSetFilter}) that lists
 *   and matches *individual* values (a row matches if it shares ANY selected
 *   value), while the Text Filter still works over the joined label;
 * - `meta.filterOptions` = the distinct individual values across the data, so
 *   the set panel's checkbox list shows tokens rather than whole combinations.
 *
 * The accessor joins values with ", " for global search, sorting, and CSV
 * export — matching how these columns read today.
 *
 * @example
 * createCsvColumn<ProjectListDto>({
 *   id: 'projectManagers',
 *   header: 'PMs',
 *   data: projects,
 *   getValues: (row) => getSortedNameList(row.projectManagers ?? []),
 * })
 */
export const createCsvColumn = <T,>(
  options: CsvColumnOptions<T>,
): ColumnDef<T, any> => {
  const { id, header, size, data } = options
  const getValues =
    options.getValues ??
    ((row: T) => splitCsv((row as Record<string, unknown>)[id] as string))

  const filterOptions = deriveFilterOptions(data, getValues)

  return {
    id,
    header,
    size,
    accessorFn: (row) => getValues(row).join(', '),
    filterFn: createMultiValueSetFilter<T>(getValues),
    meta: { filterType: 'set', filterOptions },
    cell: ({ row }) => <TagListCell values={getValues(row.original)} />,
  }
}

/** Distinct individual values across the data, sorted case-insensitively, as
 *  set-filter checkbox options. */
const deriveFilterOptions = <T,>(
  data: T[] | undefined,
  getValues: (row: T) => string[],
): FilterOption[] => {
  const names = new Set<string>()
  for (const row of data ?? []) {
    for (const value of getValues(row)) {
      names.add(value)
    }
  }
  return Array.from(names)
    .sort(caseInsensitiveCompare)
    .map((name) => ({ label: name, value: name }))
}
