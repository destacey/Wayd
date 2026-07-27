import dayjs from 'dayjs'

/**
 * Leading characters that make a spreadsheet parse a cell as a formula. `=HYPERLINK("http://evil")`
 * executes on open, and quoting does not defuse it — the cell has to stop looking like a formula.
 */
const FORMULA_TRIGGERS = ['=', '+', '-', '@', '\t', '\r']

/** Strings only: a numeric `-5` is data the spreadsheet should still read as a number. */
const isFormula = (value: unknown, str: string): boolean =>
  typeof value === 'string' && FORMULA_TRIGGERS.includes(str[0])

/**
 * Escapes a value for CSV format
 */
export const escapeCsv = (value: unknown): string => {
  const str = value == null ? '' : String(value)
  // A leading apostrophe means "literal text" and is consumed on import.
  const defused = isFormula(value, str) ? `'${str}` : str
  const escaped = defused.replace(/\"/g, '""')
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
  const BOM = '\uFEFF'
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
