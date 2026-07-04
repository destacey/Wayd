import {
  caseInsensitiveCompare,
  dateSortBy,
  sortEmptyLast,
  workItemKeySort,
  workStatusCategorySort,
} from './grid-sorting'
import type { Row } from '@tanstack/react-table'

describe('grid-sorting', () => {
  describe('caseInsensitiveCompare', () => {
    it('interleaves capitalized and lowercase values alphabetically', () => {
      // Arrange — plain Array.sort() would yield ['Bug', 'Chore', 'api']
      const values = ['Chore', 'api', 'Bug']

      // Act
      const sorted = [...values].sort(caseInsensitiveCompare)

      // Assert
      expect(sorted).toEqual(['api', 'Bug', 'Chore'])
    })

    it('compares numeric segments numerically', () => {
      // Arrange — plain Array.sort() would put 'v10' before 'v9'
      const values = ['v10', 'v9', 'v1']

      // Act
      const sorted = [...values].sort(caseInsensitiveCompare)

      // Assert
      expect(sorted).toEqual(['v1', 'v9', 'v10'])
    })
  })

  describe('dateSortBy', () => {
    const row = (value?: string | number | Date | null): Row<any> =>
      ({ original: { value } }) as unknown as Row<any>

    it('returns 0 for equal dates', () => {
      const sort = dateSortBy((r: any) => r.original.value)

      expect(sort(row('2024-01-01'), row('2024-01-01'), 'x')).toBe(0)
    })

    it('sorts earlier dates before later dates', () => {
      const sort = dateSortBy((r: any) => r.original.value)

      expect(sort(row('2024-01-01'), row('2024-01-02'), 'x')).toBeLessThan(0)
      expect(sort(row('2024-01-02'), row('2024-01-01'), 'x')).toBeGreaterThan(0)
    })

    it('supports Date and numeric timestamps', () => {
      const sort = dateSortBy((r: any) => r.original.value)

      const jan1 = new Date('2024-01-01T00:00:00Z')
      const jan2 = new Date('2024-01-02T00:00:00Z')
      const jan1Ms = jan1.getTime()

      expect(sort(row(jan1), row(jan2), 'x')).toBeLessThan(0)
      expect(sort(row(jan1Ms), row(jan2), 'x')).toBeLessThan(0)
      expect(sort(row(jan1Ms), row(jan1), 'x')).toBe(0)
    })

    it('treats null/undefined as emptyValue (default: -Infinity), sorting empties first', () => {
      const sort = dateSortBy((r: any) => r.original.value)

      expect(sort(row(null), row('2024-01-01'), 'x')).toBeLessThan(0)
      expect(sort(row(undefined), row('2024-01-01'), 'x')).toBeLessThan(0)
      expect(sort(row(null), row(undefined), 'x')).toBe(0)
    })

    it('respects emptyValue override', () => {
      const sort = dateSortBy((r: any) => r.original.value, {
        emptyValue: Infinity,
      })

      expect(sort(row(null), row('2024-01-01'), 'x')).toBeGreaterThan(0)
      expect(sort(row(undefined), row('2024-01-01'), 'x')).toBeGreaterThan(0)
      expect(sort(row(null), row(undefined), 'x')).toBe(0)
    })
  })

  describe('sortEmptyLast', () => {
    /** Minimal Row stub exposing getValue for a single column. */
    const row = (value: unknown) =>
      ({ getValue: () => value }) as unknown as Parameters<
        typeof sortEmptyLast
      >[0]

    const cmp = (a: unknown, b: unknown) => sortEmptyLast(row(a), row(b), 'x')

    describe('empties are treated as the largest value', () => {
      it.each([
        ['null', null],
        ['undefined', undefined],
        ['empty string', ''],
      ])(
        'sorts %s after a real value (positive when a is empty)',
        (_label, empty) => {
          // Act / Assert — a empty vs b=5 → a sorts after b (ascending: last)
          expect(cmp(empty, 5)).toBe(1)
          expect(cmp(5, empty)).toBe(-1)
        },
      )

      it('treats two empties as equal', () => {
        // Act / Assert
        expect(cmp(null, undefined)).toBe(0)
        expect(cmp('', null)).toBe(0)
      })
    })

    describe('numeric comparison for non-empty numbers', () => {
      it('orders numbers ascending', () => {
        // Act / Assert
        expect(cmp(1, 2)).toBe(-1)
        expect(cmp(2, 1)).toBe(1)
        expect(cmp(2, 2)).toBe(0)
      })

      it('compares numeric strings numerically (10 after 9, not before)', () => {
        // Act / Assert — string compare would put '10' before '9'
        expect(cmp('9', '10')).toBe(-1)
      })

      it('does not treat booleans as numbers', () => {
        // Act / Assert — true/false must not coerce to 1/0 and compare numerically
        const result = cmp(true, false)
        expect(typeof result).toBe('number')
      })
    })

    describe('string comparison for non-numeric values', () => {
      it('orders strings case-insensitively', () => {
        // Act / Assert
        expect(cmp('apple', 'Banana')).toBe(-1)
        expect(cmp('Banana', 'apple')).toBe(1)
      })
    })

    describe('sorted end-to-end', () => {
      it('puts empties last on an ascending sort of a mixed list', () => {
        // Arrange
        const values = [3, null, 1, undefined, 2, '']

        // Act — ascending
        const sorted = [...values].sort((a, b) =>
          sortEmptyLast(row(a), row(b), 'x'),
        )

        // Assert — real values ascending, all empties trailing
        expect(sorted.slice(0, 3)).toEqual([1, 2, 3])
        expect(
          sorted
            .slice(3)
            .every((v) => v === null || v === undefined || v === ''),
        ).toBe(true)
      })
    })
  })

  describe('workItemKeySort', () => {
    const row = (value: unknown) =>
      ({ getValue: () => value }) as unknown as Row<any>
    const cmp = (a: unknown, b: unknown) => workItemKeySort(row(a), row(b), 'x')

    it('sorts by numeric suffix within a prefix (WEB-9 before WEB-10)', () => {
      // Act / Assert — a plain string sort would put WEB-10 before WEB-9
      expect(cmp('WEB-9', 'WEB-10')).toBeLessThan(0)
      expect(cmp('WEB-10', 'WEB-9')).toBeGreaterThan(0)
      expect(cmp('WEB-5', 'WEB-5')).toBe(0)
    })

    it('sorts by prefix alphabetically first', () => {
      // Act / Assert
      expect(cmp('API-100', 'WEB-1')).toBeLessThan(0)
      expect(cmp('WEB-1', 'API-100')).toBeGreaterThan(0)
    })

    it('sorts empty keys to the end (ascending)', () => {
      // Act / Assert
      expect(cmp('', 'WEB-1')).toBe(1)
      expect(cmp('WEB-1', '')).toBe(-1)
      expect(cmp(null, undefined)).toBe(0)
    })

    it('orders a mixed list correctly end-to-end', () => {
      // Arrange
      const keys = ['WEB-10', 'API-2', 'WEB-2', '', 'API-10']

      // Act
      const sorted = [...keys].sort((a, b) => cmp(a, b))

      // Assert — API before WEB, numeric within prefix, empty last
      expect(sorted).toEqual(['API-2', 'API-10', 'WEB-2', 'WEB-10', ''])
    })

    it('falls back to a string compare for a malformed suffix (no NaN)', () => {
      // Arrange / Act / Assert — a non-numeric suffix must not produce NaN,
      // which would make the comparator return an unstable value.
      expect(cmp('WEB-abc', 'WEB-def')).toBeLessThan(0)
      expect(cmp('WEB-abc', 'WEB-abc')).toBe(0)
      expect(Number.isNaN(cmp('WEB-x', 'WEB-1'))).toBe(false)
    })
  })

  describe('workStatusCategorySort', () => {
    const row = (value: unknown) =>
      ({ getValue: () => value }) as unknown as Row<any>
    const cmp = (a: unknown, b: unknown) =>
      workStatusCategorySort(row(a), row(b), 'x')

    it('orders by workflow position, not alphabetically', () => {
      // Act / Assert — Proposed < Active < Done < Removed
      expect(cmp('Proposed', 'Active')).toBeLessThan(0)
      expect(cmp('Active', 'Done')).toBeLessThan(0)
      expect(cmp('Done', 'Removed')).toBeLessThan(0)
      // Alphabetically 'Active' < 'Done' < 'Proposed' < 'Removed' — the workflow
      // order deliberately differs.
      expect(cmp('Removed', 'Active')).toBeGreaterThan(0)
    })

    it('sorts a mixed list into workflow order', () => {
      // Arrange
      const categories = ['Done', 'Proposed', 'Removed', 'Active']

      // Act
      const sorted = [...categories].sort((a, b) => cmp(a, b))

      // Assert
      expect(sorted).toEqual(['Proposed', 'Active', 'Done', 'Removed'])
    })

    it('sorts unknown/blank categories last, not first', () => {
      // Arrange — a raw indexOf would give unknowns -1 and float them to the top
      const categories = ['Done', 'Mystery', 'Proposed', '', 'Active']

      // Act
      const sorted = [...categories].sort((a, b) => cmp(a, b))

      // Assert — known categories in workflow order, unknowns trailing
      expect(sorted.slice(0, 3)).toEqual(['Proposed', 'Active', 'Done'])
      expect(sorted.slice(3).every((c) => c === 'Mystery' || c === '')).toBe(
        true,
      )
    })
  })
})
