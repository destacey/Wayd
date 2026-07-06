import type { Column, Table } from '@tanstack/react-table'
import {
  escapeCsv,
  generateCsv,
  downloadCsvWithTimestamp,
} from '@/src/utils/csv-utils'
import { getOrderedVisibleLeafColumns } from './column-order'

/** Header text for a column: meta.exportHeader → string header → column id. */
const resolveExportHeader = <T>(column: Column<T, unknown>): string => {
  const { meta, header } = column.columnDef
  if (meta?.exportHeader) return meta.exportHeader
  if (typeof header === 'string') return header
  return column.id
}

/** The column's ancestor group at the given band depth (0 = outermost), or
 *  undefined when the column isn't nested that deep. */
const ancestorAtDepth = <T>(
  column: Column<T, unknown>,
  depth: number,
): Column<T, unknown> | undefined => {
  const chain: Column<T, unknown>[] = []
  let current = column.parent
  while (current) {
    chain.unshift(current)
    current = current.parent
  }
  return chain[depth]
}

/**
 * Exports a grid to CSV and triggers the download (filename gets a timestamp).
 *
 * Exports only what's on screen: the currently *visible* leaf columns in their
 * displayed order (so hidden columns are excluded and reordered/pinned columns
 * export in the order the user sees) and the *filtered/sorted* rows
 * (getRowModel). Values come from TanStack's own accessors (row.getValue), so nested
 * accessorKeys like `status.name` resolve correctly and column-type
 * transforms (e.g. yesNo's boolean → "Yes"/"No") are reflected.
 *
 * Grouped headers export as prelude rows above the leaf header row — one row
 * per band level, each group's label emitted at its first exported column and
 * blank across the rest of its span (mirroring ag-grid's CSV group headers).
 *
 * Column meta hooks: `enableExport: false` excludes a column, `exportHeader`
 * overrides the header text, and `exportFormatter` maps each value.
 */
export function exportGridToCsv<T>(table: Table<T>, csvFileName: string): void {
  const exportableColumns = getOrderedVisibleLeafColumns(table).filter(
    (column) => {
      const meta = column.columnDef.meta
      if (meta?.enableExport === false) return false
      // Only columns with an accessor produce a value worth exporting.
      return column.accessorFn != null
    },
  )

  const headers = exportableColumns.map(resolveExportHeader)

  // Group-header prelude rows: a leaf column's `depth` is its ancestor-group
  // count, so the deepest exported leaf sets how many band levels to write.
  // (Derived from the column tree, not getHeaderGroups(), which needs pinning
  // state that headless createTable harnesses don't carry.)
  const bandLevelCount = Math.max(
    0,
    ...exportableColumns.map((column) => column.depth),
  )
  const groupHeaderRows: string[][] = []
  for (let level = 0; level < bandLevelCount; level++) {
    let previousGroup: Column<T, unknown> | undefined
    groupHeaderRows.push(
      exportableColumns.map((column) => {
        const group = ancestorAtDepth(column, level)
        const label =
          group && group !== previousGroup ? resolveExportHeader(group) : ''
        previousGroup = group
        return label
      }),
    )
  }

  const exportRows = table.getRowModel().rows.map((row) =>
    exportableColumns.map((column) => {
      const meta = column.columnDef.meta
      const value = row.getValue(column.id)
      if (meta?.exportFormatter) {
        return meta.exportFormatter(value, row.original)
      }
      return value ?? ''
    }),
  )

  const csvBody = generateCsv(headers, exportRows)
  const csv =
    groupHeaderRows.length === 0
      ? csvBody
      : [
          ...groupHeaderRows.map((row) => row.map(escapeCsv).join(',')),
          csvBody,
        ].join('\n')
  downloadCsvWithTimestamp(csv, csvFileName)
}
