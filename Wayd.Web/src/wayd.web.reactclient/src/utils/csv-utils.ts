import dayjs from 'dayjs'

/**
 * Escapes a value for CSV format
 */
export const escapeCsv = (value: unknown): string => {
  const str = value == null ? '' : String(value)
  const escaped = str.replace(/\"/g, '""')
  return /[\",\n\r]/.test(escaped) ? `"${escaped}"` : escaped
}

/**
 * Generates CSV content from headers and rows
 */
export const generateCsv = (headers: string[], rows: unknown[][]): string => {
  const csvRows = [
    headers.map(escapeCsv).join(','),
    ...rows.map((r) => r.map(escapeCsv).join(',')),
  ]
  return csvRows.join('\n')
}

/**
 * Downloads CSV file
 */
export const downloadCsv = (csvContent: string, filename: string): void => {
  // Prepend a UTF-8 BOM so spreadsheet apps (notably Excel) detect UTF-8 and
  // render multibyte characters (accents, emoji/icons) correctly instead of
  // falling back to a legacy code page (which mojibakes e.g. 🚀 → "ðŸš€").
  const BOM = '﻿'
  const blob = new Blob([BOM, csvContent], { type: 'text/csv;charset=utf-8;' })
  const url = URL.createObjectURL(blob)

  const link = document.createElement('a')
  link.href = url
  link.download = filename
  link.click()

  URL.revokeObjectURL(url)
}

/**
 * Downloads CSV file with timestamp
 */
export const downloadCsvWithTimestamp = (
  csvContent: string,
  baseFilename: string,
): void => {
  const filename = `${baseFilename}-${dayjs().format('YYYY-MM-DD')}.csv`
  downloadCsv(csvContent, filename)
}
