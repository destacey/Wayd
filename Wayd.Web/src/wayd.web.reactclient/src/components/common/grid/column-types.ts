import { ColTypeDef } from 'ag-grid-community'
import dayjs from 'dayjs'

const DATE_ONLY_FORMAT = 'MMM D, YYYY'

/**
 * Formats a date-only value (a JS `Date` or ISO string) as `MMM D, YYYY`.
 * Returns an empty string for null/undefined.
 */
const formatDateOnly = (value: unknown): string =>
  value ? dayjs(value as Date | string).format(DATE_ONLY_FORMAT) : ''

/**
 * Shared AG Grid column types, registered on WaydGrid via the `columnTypes`
 * grid option. Reference from a column with `type: 'dateOnly'`.
 */
export const waydColumnTypes: Record<string, ColTypeDef> = {
  /**
   * Date-only column. Displays `MMM D, YYYY` while keeping the raw value for
   * sorting, so chronological sort is correct. Uses agDateColumnFilter so the
   * floating filter is a real date picker, and routes the formatted text to
   * the global quick filter so search matches what the user sees on screen.
   */
  dateOnly: {
    valueFormatter: (params) => formatDateOnly(params.value),
    getQuickFilterText: (params) => formatDateOnly(params.value),
    filter: 'agDateColumnFilter',
    filterParams: {
      // Compare the cell value (Date or ISO string) against the picker's
      // local Date at day granularity.
      comparator: (filterDate: Date, cellValue: unknown) => {
        if (!cellValue) return -1
        const cell = dayjs(cellValue as Date | string).startOf('day')
        const filter = dayjs(filterDate).startOf('day')
        if (cell.isSame(filter)) return 0
        return cell.isBefore(filter) ? -1 : 1
      },
    },
  },
}

// Re-exported so non-grid callers (and columns that need a custom empty-value
// fallback) can share the exact same formatting as the grid columns.
export { formatDateOnly, DATE_ONLY_FORMAT }
