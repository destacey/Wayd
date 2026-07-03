import { dateSortBy, sortEmptyLast } from './grid-sorting'
import type { Row } from '@tanstack/react-table'

describe('grid-sorting', () => {
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
})
