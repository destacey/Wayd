'use client'

import type { ColumnDef } from '@tanstack/react-table'

import { createMultiValueSetFilter } from './filters'
import TagListCell from './tag-list-cell'

/**
 * Splits a comma-separated string into its trimmed, non-empty values. The
 * default {@link CsvColumnOptions.getValues} for columns whose underlying data
 * is a single CSV string (e.g. `"Alice, Bob"`), and the inverse of the ", "
 * join the factory's accessor uses — so the grid can recover a row's individual
 * tokens from its faceted joined value.
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
 * It bundles the pieces such a column always needs, so callers don't hand-roll
 * them per model:
 * - a **tag-list cell** ({@link TagListCell}) rendering each value as a Tag;
 * - a **multi-value set filter** ({@link createMultiValueSetFilter}) that
 *   matches on *individual* values (a row matches if it shares ANY selected
 *   value), while the Text Filter still works over the joined label;
 * - `meta.multiValueSplit`, which tells the grid to build the set panel's
 *   checkbox list from the individual tokens faceted out of the live data
 *   rather than the whole joined string — so no data or option list is passed
 *   in, and the options always reflect the rows actually shown.
 *
 * The accessor joins values with ", " for global search, sorting, and CSV
 * export — matching how these columns read today.
 *
 * @example
 * createCsvColumn<ProjectListDto>({
 *   id: 'projectManagers',
 *   header: 'PMs',
 *   getValues: (row) => getSortedNameList(row.projectManagers ?? []),
 * })
 */
export const createCsvColumn = <T,>(
  options: CsvColumnOptions<T>,
): ColumnDef<T, any> => {
  const { id, header, size } = options
  const getValues =
    options.getValues ??
    ((row: T) => splitCsv((row as Record<string, unknown>)[id] as string))

  return {
    id,
    header,
    size,
    accessorFn: (row) => getValues(row).join(', '),
    filterFn: createMultiValueSetFilter<T>(getValues),
    meta: { filterType: 'set', multiValueSplit: splitCsv },
    cell: ({ row }) => <TagListCell values={getValues(row.original)} />,
  }
}
