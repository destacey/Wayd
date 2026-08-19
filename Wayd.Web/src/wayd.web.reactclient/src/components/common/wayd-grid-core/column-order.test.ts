import type { ColumnDef } from './index'
import type { TableState } from './index'

import { buildHeadlessTable } from './test-table'

import { reconcileColumnOrder, reorderIds } from './column-order'

describe('column-order', () => {
  describe('reconcileColumnOrder', () => {
    it('returns the def order when the stored order is empty', () => {
      // Arrange
      const current = ['a', 'b', 'c']

      // Act
      const result = reconcileColumnOrder([], current)

      // Assert
      expect(result).toEqual(['a', 'b', 'c'])
    })

    it('preserves a stored reordering of unchanged columns', () => {
      // Arrange
      const stored = ['c', 'a', 'b']
      const current = ['a', 'b', 'c']

      // Act
      const result = reconcileColumnOrder(stored, current)

      // Assert
      expect(result).toEqual(['c', 'a', 'b'])
    })

    it('drops stored ids for columns that no longer exist', () => {
      // Arrange — 'x' was removed from the defs
      const stored = ['c', 'x', 'a', 'b']
      const current = ['a', 'b', 'c']

      // Act
      const result = reconcileColumnOrder(stored, current)

      // Assert
      expect(result).toEqual(['c', 'a', 'b'])
    })

    it('inserts a newly added column at its def position, not the end', () => {
      // Arrange — 'b' is new (stored order predates it); def order is a,b,c,d
      const stored = ['d', 'a', 'c']
      const current = ['a', 'b', 'c', 'd']

      // Act
      const result = reconcileColumnOrder(stored, current)

      // Assert — 'b' lands between 'a' and 'c' (its def neighbours), while the
      // stored reordering of the others is preserved
      expect(result).toEqual(['d', 'a', 'b', 'c'])
    })

    it('places a new first-def column ahead of its def-later neighbour', () => {
      // Arrange — 'a' is new and sits first in the defs
      const stored = ['c', 'b']
      const current = ['a', 'b', 'c']

      // Act
      const result = reconcileColumnOrder(stored, current)

      // Assert — 'a' precedes 'b' (its only already-placed def-later neighbour)
      expect(result).toEqual(['a', 'c', 'b'])
    })

    it('appends a new last-def column after all stored ids', () => {
      // Arrange — 'c' is new and sits last in the defs
      const stored = ['b', 'a']
      const current = ['a', 'b', 'c']

      // Act
      const result = reconcileColumnOrder(stored, current)

      // Assert
      expect(result).toEqual(['b', 'a', 'c'])
    })
  })

  describe('reorderIds', () => {
    it('moves the active id to the over id position', () => {
      // Arrange / Act
      const result = reorderIds(['a', 'b', 'c', 'd'], 'b', 'd')

      // Assert
      expect(result).toEqual(['a', 'c', 'd', 'b'])
    })

    it('returns the same reference for a no-op move (same id)', () => {
      // Arrange
      const ids = ['a', 'b', 'c']

      // Act
      const result = reorderIds(ids, 'b', 'b')

      // Assert
      expect(result).toBe(ids)
    })

    it('returns the same reference when either id is absent', () => {
      // Arrange
      const ids = ['a', 'b', 'c']

      // Act / Assert
      expect(reorderIds(ids, 'z', 'a')).toBe(ids)
      expect(reorderIds(ids, 'a', 'z')).toBe(ids)
    })
  })

  // Guards the composition the whole feature rests on: columnOrder governs the
  // center section only; each pinned section follows its columnPinning array.
  describe('columnOrder × pinning composition (TanStack)', () => {
    type Item = { a: string; b: string; c: string; d: string }
    const data: Item[] = [{ a: '1', b: '2', c: '3', d: '4' }]
    const columns: ColumnDef<Item, any>[] = [
      { id: 'a', accessorKey: 'a', header: 'A' },
      { id: 'b', accessorKey: 'b', header: 'B' },
      { id: 'c', accessorKey: 'c', header: 'C' },
      { id: 'd', accessorKey: 'd', header: 'D' },
    ]

    const buildTable = (state: Partial<TableState>) =>
      buildHeadlessTable<Item>(data, columns, {
        columnPinning: { start: [], end: [] },
        columnOrder: [],
        ...state,
      })

    /** Rendered leaf order the grid's colgroup uses. */
    const renderedOrder = (table: ReturnType<typeof buildTable>) =>
      [
        ...table.getStartVisibleLeafColumns(),
        ...table.getCenterVisibleLeafColumns(),
        ...table.getEndVisibleLeafColumns(),
      ].map((col) => col.id)

    it('reorders the center section by columnOrder', () => {
      // Arrange / Act
      const table = buildTable({ columnOrder: ['c', 'a', 'b', 'd'] })

      // Assert
      expect(renderedOrder(table)).toEqual(['c', 'a', 'b', 'd'])
    })

    it('orders pinned sections by the pin array, not columnOrder', () => {
      // Arrange — pin d then a left; columnOrder tries the opposite order
      const table = buildTable({
        columnPinning: { start: ['d', 'a'], end: [] },
        columnOrder: ['a', 'b', 'c', 'd'],
      })

      // Act / Assert — left section follows the pin array [d, a]; center keeps
      // columnOrder for the rest
      expect(renderedOrder(table)).toEqual(['d', 'a', 'b', 'c'])
    })

    it('applies columnOrder only within the center when some columns are pinned', () => {
      // Arrange — b pinned right; center order reversed
      const table = buildTable({
        columnPinning: { start: [], end: ['b'] },
        columnOrder: ['d', 'c', 'a', 'b'],
      })

      // Act / Assert — center is [d, c, a] (columnOrder minus pinned), b sticks right
      expect(renderedOrder(table)).toEqual(['d', 'c', 'a', 'b'])
    })
  })
})
