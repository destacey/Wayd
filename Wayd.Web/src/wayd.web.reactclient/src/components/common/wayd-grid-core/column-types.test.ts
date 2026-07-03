// The global jest.setup dayjs mock returns raw values; the formatter tests
// below need the real library (same opt-in as the filter suites).
jest.mock('dayjs', () => jest.requireActual('dayjs'))

import type { ColumnDef } from '@tanstack/react-table'

import {
  applyColumnType,
  formatDateOnly,
  formatDateTime,
  YES,
  NO,
  YES_NO_COLUMN_SIZE,
} from './column-types'
import type { WaydGridColumnMeta } from './types'

interface Row {
  flag: boolean | null | undefined
  when: string | null
}

/** Invoke a resolved column's accessorFn (TanStack passes row, index). */
const access = (col: ColumnDef<Row, unknown>, row: Row) =>
  (col as { accessorFn: (r: Row, i: number) => unknown }).accessorFn(row, 0)

describe('applyColumnType', () => {
  it('returns the column unchanged when no columnType is set', () => {
    // Arrange
    const col: ColumnDef<Row, unknown> = { id: 'flag', accessorKey: 'flag' }

    // Act
    const result = applyColumnType(col)

    // Assert
    expect(result).toBe(col)
  })

  describe('yesNo', () => {
    const col: ColumnDef<Row, unknown> = {
      id: 'flag',
      accessorKey: 'flag',
      meta: { columnType: 'yesNo' },
    }
    const resolved = applyColumnType(col)

    it('maps the boolean accessor to "Yes" / "No" (capital Y / N)', () => {
      // Arrange / Act / Assert
      expect(access(resolved, { flag: true, when: null })).toBe(YES)
      expect(access(resolved, { flag: false, when: null })).toBe(NO)
      expect(YES).toBe('Yes')
      expect(NO).toBe('No')
    })

    it('maps null / undefined to blank', () => {
      // Arrange / Act / Assert
      expect(access(resolved, { flag: null, when: null })).toBe('')
      expect(access(resolved, { flag: undefined, when: null })).toBe('')
    })

    it('sets a set filter offering Yes / No, not true / false', () => {
      // Arrange / Act / Assert
      const meta = resolved.meta as WaydGridColumnMeta
      expect(meta.filterType).toBe('set')
      expect(meta.filterOptions).toEqual([
        { label: 'Yes', value: 'Yes' },
        { label: 'No', value: 'No' },
      ])
    })

    it('applies the default yes-no width when the column sets no size', () => {
      // Arrange / Act / Assert
      expect(resolved.size).toBe(YES_NO_COLUMN_SIZE)
    })

    it('uses a set filter (Excel-style checkbox list of False/True)', () => {
      // Arrange / Act / Assert — booleans are a normal 2-item set, no special
      // single-select mode.
      expect((resolved.meta as WaydGridColumnMeta).filterType).toBe('set')
    })
  })

  describe('dateOnly / dateTime', () => {
    it('keeps the raw value as the accessor (for sort/filter) — no transform', () => {
      // Arrange
      const col: ColumnDef<Row, unknown> = {
        id: 'when',
        accessorKey: 'when',
        meta: { columnType: 'dateOnly' },
      }

      // Act
      const resolved = applyColumnType(col)

      // Assert — accessorKey retained (raw value flows through), date filter set
      expect((resolved as { accessorKey?: unknown }).accessorKey).toBe('when')
      expect((resolved.meta as WaydGridColumnMeta).filterType).toBe('date')
    })

    it('uses the dateTime filter type for dateTime columns', () => {
      // Arrange
      const col: ColumnDef<Row, unknown> = {
        id: 'when',
        accessorKey: 'when',
        meta: { columnType: 'dateTime' },
      }

      // Act
      const resolved = applyColumnType(col)

      // Assert
      expect((resolved.meta as WaydGridColumnMeta).filterType).toBe('dateTime')
    })

    it('exposes the standard display formatters for custom cells', () => {
      // Arrange
      const when = '2026-07-02T13:05:00Z'

      // Act / Assert — same formats the dateOnly/dateTime cells use
      expect(formatDateOnly(when)).toBe(formatDateOnly(new Date(when)))
      expect(formatDateTime(when)).toMatch(
        /^[A-Z][a-z]{2} \d{1,2}, \d{4} \d{2}:\d{2} [AP]M$/,
      )
      expect(formatDateOnly(null)).toBe('')
      expect(formatDateTime(undefined)).toBe('')
    })
  })

  describe('precedence', () => {
    it('does not override an explicit filterType on the column', () => {
      // Arrange — yesNo would default to 'set'; column forces 'text'
      const col: ColumnDef<Row, unknown> = {
        id: 'flag',
        accessorKey: 'flag',
        meta: { columnType: 'yesNo', filterType: 'text' },
      }

      // Act
      const resolved = applyColumnType(col)

      // Assert
      expect((resolved.meta as WaydGridColumnMeta).filterType).toBe('text')
    })

    it('does not override an explicit size on the column', () => {
      // Arrange — yesNo defaults to YES_NO_COLUMN_SIZE; column forces 200
      const col: ColumnDef<Row, unknown> = {
        id: 'flag',
        accessorKey: 'flag',
        size: 200,
        meta: { columnType: 'yesNo' },
      }

      // Act
      const resolved = applyColumnType(col)

      // Assert
      expect(resolved.size).toBe(200)
    })

    it('does not override an explicit cell renderer', () => {
      // Arrange
      const customCell = () => 'custom'
      const col: ColumnDef<Row, unknown> = {
        id: 'when',
        accessorKey: 'when',
        cell: customCell,
        meta: { columnType: 'dateOnly' },
      }

      // Act
      const resolved = applyColumnType(col)

      // Assert
      expect(resolved.cell).toBe(customCell)
    })
  })
})
