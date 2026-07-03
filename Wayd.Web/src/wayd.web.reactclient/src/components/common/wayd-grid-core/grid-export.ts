import type { Table } from '@tanstack/react-table'
import { generateCsv, downloadCsvWithTimestamp } from '@/src/utils/csv-utils'

/**
 * Exports a grid to CSV and triggers the download (filename gets a timestamp).
 *
 * Exports only what's on screen: the currently *visible* leaf columns (so
 * hidden columns are excluded) and the *filtered/sorted* rows (getRowModel).
 * Values come from TanStack's own accessors (row.getValue), so nested
 * accessorKeys like `status.name` resolve correctly and column-type
 * transforms (e.g. yesNo's boolean → "Yes"/"No") are reflected.
 *
 * Column meta hooks: `enableExport: false` excludes a column, `exportHeader`
 * overrides the header text, and `exportFormatter` maps each value.
 */
export function exportGridToCsv<T>(table: Table<T>, csvFileName: string): void {
  const exportableColumns = table.getVisibleLeafColumns().filter((column) => {
    const meta = column.columnDef.meta
    if (meta?.enableExport === false) return false
    // Only columns with an accessor produce a value worth exporting.
    return column.accessorFn != null
  })

  const headers = exportableColumns.map((column) => {
    const { meta, header } = column.columnDef
    if (meta?.exportHeader) return meta.exportHeader
    if (typeof header === 'string') return header
    return column.id
  })

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

  const csv = generateCsv(headers, exportRows)
  downloadCsvWithTimestamp(csv, csvFileName)
}
