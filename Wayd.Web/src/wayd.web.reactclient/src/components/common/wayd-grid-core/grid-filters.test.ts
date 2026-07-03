import {
  numberRangeFilter,
  setContainsFilter,
  stringContainsFilter,
} from './grid-filters'

type TestRow = {
  getValue: (columnId: string) => unknown
}

const makeRow = (values: Record<string, unknown>): TestRow => {
  return {
    getValue: (columnId: string) => values[columnId],
  }
}

describe('grid-filters', () => {
  describe('stringContainsFilter', () => {
    it('returns true for blank filter', () => {
      const row = makeRow({ name: 'Alpha' })
      expect(stringContainsFilter(row as any, 'name', '', () => {})).toBe(true)
      expect(stringContainsFilter(row as any, 'name', '   ', () => {})).toBe(
        true,
      )
      expect(
        stringContainsFilter(row as any, 'name', null as any, () => {}),
      ).toBe(true)
    })

    it('matches case-insensitively and trims filter value', () => {
      const row = makeRow({ name: 'Project Task' })
      expect(
        stringContainsFilter(row as any, 'name', '  task  ', () => {}),
      ).toBe(true)
      expect(stringContainsFilter(row as any, 'name', 'TASK', () => {})).toBe(
        true,
      )
      expect(
        stringContainsFilter(row as any, 'name', 'missing', () => {}),
      ).toBe(false)
    })

    it('stringifies non-string cell values', () => {
      const row = makeRow({ est: 12.5 })
      expect(stringContainsFilter(row as any, 'est', '12', () => {})).toBe(true)
      expect(stringContainsFilter(row as any, 'est', '12.5', () => {})).toBe(
        true,
      )
      expect(stringContainsFilter(row as any, 'est', '13', () => {})).toBe(
        false,
      )
    })

    it('treats null/undefined cell value as empty string', () => {
      const row = makeRow({ name: null })
      expect(stringContainsFilter(row as any, 'name', 'a', () => {})).toBe(
        false,
      )
    })

    it('updates behavior when filterValue changes (cache safety)', () => {
      const row = makeRow({ name: 'Alpha' })
      expect(stringContainsFilter(row as any, 'name', 'alp', () => {})).toBe(
        true,
      )
      expect(stringContainsFilter(row as any, 'name', 'bet', () => {})).toBe(
        false,
      )
    })
  })

  describe('setContainsFilter', () => {
    it('returns true for null/empty filter', () => {
      const row = makeRow({ status: 'Open' })
      expect(
        setContainsFilter(row as any, 'status', null as any, () => {}),
      ).toBe(true)
      expect(setContainsFilter(row as any, 'status', [], () => {})).toBe(true)
    })

    it('supports single-value filter', () => {
      const row = makeRow({ status: 'Open' })
      expect(setContainsFilter(row as any, 'status', 'Open', () => {})).toBe(
        true,
      )
      expect(setContainsFilter(row as any, 'status', 'Closed', () => {})).toBe(
        false,
      )
    })

    it('supports array (set) filter', () => {
      const rowA = makeRow({ status: 'Open' })
      const rowB = makeRow({ status: 'Closed' })
      const selected = ['Open', 'In Progress']
      expect(setContainsFilter(rowA as any, 'status', selected, () => {})).toBe(
        true,
      )
      expect(setContainsFilter(rowB as any, 'status', selected, () => {})).toBe(
        false,
      )
    })

    it('returns false when cell value is null and filter is active', () => {
      const row = makeRow({ status: null })
      expect(setContainsFilter(row as any, 'status', 'Open', () => {})).toBe(
        false,
      )
      expect(setContainsFilter(row as any, 'status', ['Open'], () => {})).toBe(
        false,
      )
    })
  })

  describe('numberRangeFilter', () => {
    it('returns true for blank or invalid filter input', () => {
      const row = makeRow({ n: 5 })
      expect(numberRangeFilter(row as any, 'n', '', () => {})).toBe(true)
      expect(numberRangeFilter(row as any, 'n', '   ', () => {})).toBe(true)
      expect(numberRangeFilter(row as any, 'n', 'nope', () => {})).toBe(true)
    })

    it('supports exact number match', () => {
      const row4 = makeRow({ n: 4 })
      const row5 = makeRow({ n: 5 })
      expect(numberRangeFilter(row4 as any, 'n', '4', () => {})).toBe(true)
      expect(numberRangeFilter(row5 as any, 'n', '4', () => {})).toBe(false)
    })

    it('supports comparisons', () => {
      const row4 = makeRow({ n: 4 })
      const row5 = makeRow({ n: 5 })

      expect(numberRangeFilter(row4 as any, 'n', '>=4', () => {})).toBe(true)
      expect(numberRangeFilter(row5 as any, 'n', '>=4', () => {})).toBe(true)
      expect(numberRangeFilter(row4 as any, 'n', '>4', () => {})).toBe(false)

      expect(numberRangeFilter(row4 as any, 'n', '<=4', () => {})).toBe(true)
      expect(numberRangeFilter(row5 as any, 'n', '<=4', () => {})).toBe(false)
      expect(numberRangeFilter(row4 as any, 'n', '<4', () => {})).toBe(false)
    })

    it('supports closed ranges (2-6 and 2..6)', () => {
      const row2 = makeRow({ n: 2 })
      const row6 = makeRow({ n: 6 })
      const row7 = makeRow({ n: 7 })

      expect(numberRangeFilter(row2 as any, 'n', '2-6', () => {})).toBe(true)
      expect(numberRangeFilter(row6 as any, 'n', '2-6', () => {})).toBe(true)
      expect(numberRangeFilter(row7 as any, 'n', '2-6', () => {})).toBe(false)

      expect(numberRangeFilter(row2 as any, 'n', '2..6', () => {})).toBe(true)
      expect(numberRangeFilter(row6 as any, 'n', '2..6', () => {})).toBe(true)
      expect(numberRangeFilter(row7 as any, 'n', '2..6', () => {})).toBe(false)
    })

    it('supports open-ended ranges (..6 and 2..)', () => {
      const row1 = makeRow({ n: 1 })
      const row6 = makeRow({ n: 6 })
      const row7 = makeRow({ n: 7 })

      expect(numberRangeFilter(row1 as any, 'n', '..6', () => {})).toBe(true)
      expect(numberRangeFilter(row6 as any, 'n', '..6', () => {})).toBe(true)
      expect(numberRangeFilter(row7 as any, 'n', '..6', () => {})).toBe(false)

      expect(numberRangeFilter(row1 as any, 'n', '2..', () => {})).toBe(false)
      expect(numberRangeFilter(row6 as any, 'n', '2..', () => {})).toBe(true)
      expect(numberRangeFilter(row7 as any, 'n', '2..', () => {})).toBe(true)
    })

    it('accepts numeric strings in the cell', () => {
      const row = makeRow({ n: '5' })
      expect(numberRangeFilter(row as any, 'n', '>=4', () => {})).toBe(true)
    })

    it('returns false for null cell values when filter is active', () => {
      const row = makeRow({ n: null })
      expect(numberRangeFilter(row as any, 'n', '>=4', () => {})).toBe(false)
    })
  })
})
