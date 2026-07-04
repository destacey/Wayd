// The global dayjs mock in jest.setup.ts is a formatting-only shim that lacks
// isValid() and full startOf/isSame/isBefore granularity. The filter engine
// needs real date math, so restore the actual dayjs for these tests.
jest.mock('dayjs', () => jest.requireActual('dayjs'))

import type { Row } from '@tanstack/react-table'

import {
  createMultiValueSetFilter,
  evaluateFilterModel,
  toDayKey,
} from './filter-engine'
import { SET_FILTER_BLANK } from './filter-model'
import type {
  ColumnFilterModel,
  DateFilterModel,
  DateSetFilterModel,
  DateTimeFilterModel,
  NumberFilterModel,
  SetFilterModel,
  TextFilterModel,
} from './filter-model'

describe('filter-engine', () => {
  describe('evaluateFilterModel — text', () => {
    const model = (
      conditions: TextFilterModel['conditions'],
      join: TextFilterModel['join'] = 'AND',
    ): TextFilterModel => ({ type: 'text', conditions, join })

    it('passes all rows when there are no active conditions', () => {
      // Arrange
      const m = model([{ op: 'contains', value: '' }])

      // Act / Assert
      expect(evaluateFilterModel(m, 'anything')).toBe(true)
    })

    it('matches contains case-insensitively', () => {
      // Arrange
      const m = model([{ op: 'contains', value: 'PLAN' }])

      // Act / Assert
      expect(evaluateFilterModel(m, 'planning-poker')).toBe(true)
      expect(evaluateFilterModel(m, 'roadmap')).toBe(false)
    })

    it('supports equals, startsWith, endsWith, notContains', () => {
      // Arrange / Act / Assert
      expect(
        evaluateFilterModel(model([{ op: 'equals', value: 'abc' }]), 'abc'),
      ).toBe(true)
      expect(
        evaluateFilterModel(model([{ op: 'equals', value: 'abc' }]), 'abcd'),
      ).toBe(false)
      expect(
        evaluateFilterModel(model([{ op: 'startsWith', value: 'road' }]), 'roadmap'),
      ).toBe(true)
      expect(
        evaluateFilterModel(model([{ op: 'endsWith', value: 'map' }]), 'roadmap'),
      ).toBe(true)
      expect(
        evaluateFilterModel(model([{ op: 'notContains', value: 'x' }]), 'roadmap'),
      ).toBe(true)
    })

    it('handles blank / notBlank without a value', () => {
      // Arrange / Act / Assert
      expect(evaluateFilterModel(model([{ op: 'blank', value: '' }]), '')).toBe(
        true,
      )
      expect(evaluateFilterModel(model([{ op: 'blank', value: '' }]), 'x')).toBe(
        false,
      )
      expect(
        evaluateFilterModel(model([{ op: 'notBlank', value: '' }]), 'x'),
      ).toBe(true)
    })

    it('combines two conditions with AND vs OR', () => {
      // Arrange
      const and = model(
        [
          { op: 'startsWith', value: 'road' },
          { op: 'endsWith', value: 'zzz' },
        ],
        'AND',
      )
      const or = model(
        [
          { op: 'startsWith', value: 'road' },
          { op: 'endsWith', value: 'zzz' },
        ],
        'OR',
      )

      // Act / Assert
      expect(evaluateFilterModel(and, 'roadmap')).toBe(false)
      expect(evaluateFilterModel(or, 'roadmap')).toBe(true)
    })
  })

  describe('evaluateFilterModel — number', () => {
    const model = (
      conditions: NumberFilterModel['conditions'],
      join: NumberFilterModel['join'] = 'AND',
    ): NumberFilterModel => ({ type: 'number', conditions, join })

    it('supports comparison operators', () => {
      // Arrange / Act / Assert
      expect(
        evaluateFilterModel(model([{ op: 'greaterThan', value: 5 }]), 6),
      ).toBe(true)
      expect(
        evaluateFilterModel(model([{ op: 'greaterThan', value: 5 }]), 5),
      ).toBe(false)
      expect(
        evaluateFilterModel(model([{ op: 'lessThanOrEqual', value: 5 }]), 5),
      ).toBe(true)
      expect(evaluateFilterModel(model([{ op: 'equals', value: 5 }]), 5)).toBe(
        true,
      )
      expect(evaluateFilterModel(model([{ op: 'notEqual', value: 5 }]), 6)).toBe(
        true,
      )
    })

    it('supports inRange inclusive of both ends and normalizes order', () => {
      // Arrange
      const m = model([{ op: 'inRange', value: 2, valueTo: 6 }])
      const reversed = model([{ op: 'inRange', value: 6, valueTo: 2 }])

      // Act / Assert
      expect(evaluateFilterModel(m, 2)).toBe(true)
      expect(evaluateFilterModel(m, 6)).toBe(true)
      expect(evaluateFilterModel(m, 7)).toBe(false)
      expect(evaluateFilterModel(reversed, 4)).toBe(true)
    })

    it('coerces numeric strings and rejects non-numeric cells', () => {
      // Arrange / Act / Assert
      expect(
        evaluateFilterModel(model([{ op: 'greaterThan', value: 5 }]), '6'),
      ).toBe(true)
      expect(
        evaluateFilterModel(model([{ op: 'greaterThan', value: 5 }]), 'abc'),
      ).toBe(false)
    })

    it('passes when the operand is not yet entered', () => {
      // Arrange
      const m = model([{ op: 'greaterThan', value: null }])

      // Act / Assert
      expect(evaluateFilterModel(m, 3)).toBe(true)
    })
  })

  describe('evaluateFilterModel — date (day granularity)', () => {
    const model = (
      conditions: DateFilterModel['conditions'],
      join: DateFilterModel['join'] = 'AND',
    ): DateFilterModel => ({ type: 'date', conditions, join })

    it('equals ignores time-of-day (same calendar day)', () => {
      // Arrange
      const m = model([{ op: 'equals', value: '2024-03-15' }])

      // Act / Assert
      // Use local-time inputs (no Z) so day-boundary comparisons are not
      // sensitive to the test runner's timezone.
      expect(evaluateFilterModel(m, '2024-03-15T09:30:00')).toBe(true)
      expect(evaluateFilterModel(m, '2024-03-16T09:30:00')).toBe(false)
    })

    it('supports before / after / inRange', () => {
      // Arrange / Act / Assert
      expect(
        evaluateFilterModel(model([{ op: 'before', value: '2024-03-15' }]), '2024-03-14'),
      ).toBe(true)
      expect(
        evaluateFilterModel(model([{ op: 'after', value: '2024-03-15' }]), '2024-03-16'),
      ).toBe(true)
      expect(
        evaluateFilterModel(
          model([{ op: 'inRange', value: '2024-03-01', valueTo: '2024-03-31' }]),
          '2024-03-15',
        ),
      ).toBe(true)
    })

    it('rejects invalid or missing cell dates', () => {
      // Arrange
      const m = model([{ op: 'equals', value: '2024-03-15' }])

      // Act / Assert
      expect(evaluateFilterModel(m, null)).toBe(false)
      expect(evaluateFilterModel(m, 'not-a-date')).toBe(false)
    })
  })

  describe('evaluateFilterModel — dateTime (minute granularity)', () => {
    const model = (
      conditions: DateTimeFilterModel['conditions'],
      join: DateTimeFilterModel['join'] = 'AND',
    ): DateTimeFilterModel => ({ type: 'dateTime', conditions, join })

    it('distinguishes times within the same day (unlike date)', () => {
      // Arrange
      const m = model([{ op: 'equals', value: '2024-03-15T09:30:00Z' }])

      // Act / Assert
      expect(evaluateFilterModel(m, '2024-03-15T09:30:30Z')).toBe(true) // same minute
      expect(evaluateFilterModel(m, '2024-03-15T09:31:00Z')).toBe(false) // next minute
    })

    it('supports before / after at minute precision', () => {
      // Arrange / Act / Assert
      expect(
        evaluateFilterModel(
          model([{ op: 'after', value: '2024-03-15T09:30:00Z' }]),
          '2024-03-15T09:31:00Z',
        ),
      ).toBe(true)
      expect(
        evaluateFilterModel(
          model([{ op: 'before', value: '2024-03-15T09:30:00Z' }]),
          '2024-03-15T09:29:00Z',
        ),
      ).toBe(true)
    })
  })

  describe('evaluateFilterModel — set', () => {
    const model = (values: string[]): SetFilterModel => ({ type: 'set', values })

    it('passes all rows when no values are selected', () => {
      // Arrange / Act / Assert
      expect(evaluateFilterModel(model([]), 'System')).toBe(true)
    })

    it('matches any selected value (OR semantics)', () => {
      // Arrange
      const m = model(['System', 'User'])

      // Act / Assert
      expect(evaluateFilterModel(m, 'System')).toBe(true)
      expect(evaluateFilterModel(m, 'User')).toBe(true)
      expect(evaluateFilterModel(m, 'Other')).toBe(false)
    })

    it('coerces the cell value to a string before comparing', () => {
      // Arrange
      const m = model(['1', '2'])

      // Act / Assert
      expect(evaluateFilterModel(m, 1)).toBe(true)
      expect(evaluateFilterModel(m, 3)).toBe(false)
    })

    it('rejects blank cells when a filter is active without the (Blanks) entry', () => {
      // Arrange
      const m = model(['System'])

      // Act / Assert
      expect(evaluateFilterModel(m, null)).toBe(false)
      expect(evaluateFilterModel(m, undefined)).toBe(false)
      expect(evaluateFilterModel(m, '')).toBe(false)
    })

    it('matches blank cells when the (Blanks) entry is selected', () => {
      // Arrange
      const m = model([SET_FILTER_BLANK])

      // Act / Assert
      expect(evaluateFilterModel(m, null)).toBe(true)
      expect(evaluateFilterModel(m, undefined)).toBe(true)
      expect(evaluateFilterModel(m, '')).toBe(true)
      expect(evaluateFilterModel(m, 'System')).toBe(false)
    })
  })

  describe('evaluateFilterModel — dateSet (date tree)', () => {
    const model = (values: string[]): DateSetFilterModel => ({
      type: 'dateSet',
      values,
    })

    it('passes all rows when no days are selected', () => {
      // Arrange / Act / Assert
      expect(evaluateFilterModel(model([]), '2026-06-17')).toBe(true)
    })

    it('matches a cell whose day is in the selected set (any raw shape)', () => {
      // Arrange
      const m = model(['2026-06-17', '2026-06-20'])

      // Act / Assert — plain date, ISO timestamp, and Date all normalize to day
      expect(evaluateFilterModel(m, '2026-06-17')).toBe(true)
      expect(evaluateFilterModel(m, '2026-06-17T14:05:00')).toBe(true)
      expect(evaluateFilterModel(m, new Date(2026, 5, 20))).toBe(true)
      expect(evaluateFilterModel(m, '2026-06-18')).toBe(false)
    })

    it('rejects null / invalid cells when a filter is active', () => {
      // Arrange
      const m = model(['2026-06-17'])

      // Act / Assert
      expect(evaluateFilterModel(m, null)).toBe(false)
      expect(evaluateFilterModel(m, 'not-a-date')).toBe(false)
    })
  })

  describe('createMultiValueSetFilter', () => {
    interface RoleRow {
      roles: string[]
    }

    // Minimal Row stand-in: the filter only reads `.original` and `.getValue()`.
    const fakeRow = (roles: string[]): Row<RoleRow> =>
      ({
        original: { roles },
        getValue: () => roles.join(', '),
      }) as unknown as Row<RoleRow>

    const filter = createMultiValueSetFilter<RoleRow>((row) => row.roles)
    const run = (roles: string[], model: ColumnFilterModel | undefined) =>
      filter(fakeRow(roles), 'roles', model, () => {})

    const set = (values: string[]): SetFilterModel => ({ type: 'set', values })
    const text = (value: string): TextFilterModel => ({
      type: 'text',
      conditions: [{ op: 'contains', value }],
      join: 'AND',
    })

    it('passes every row when there is no filter', () => {
      // Arrange / Act / Assert
      expect(run(['Owner'], undefined)).toBe(true)
    })

    it('passes every row when the set has no selected values', () => {
      // Arrange / Act / Assert
      expect(run(['Owner'], set([]))).toBe(true)
    })

    it('matches when the row shares ANY selected value (not the whole combo)', () => {
      // Arrange
      const m = set(['Owner'])

      // Act / Assert — a row with multiple roles still matches on one of them,
      // unlike matching the joined "Owner, Engineer" string exactly.
      expect(run(['Owner', 'Engineer'], m)).toBe(true)
      expect(run(['Engineer'], m)).toBe(false)
      expect(run([], m)).toBe(false)
    })

    it('matches when any of several selected values is present', () => {
      // Arrange
      const m = set(['Owner', 'Scrum Master'])

      // Act / Assert
      expect(run(['Engineer', 'Scrum Master'], m)).toBe(true)
      expect(run(['Engineer'], m)).toBe(false)
    })

    it('matches value-less rows only via the (Blanks) entry', () => {
      // Arrange / Act / Assert — an empty list is a blank row
      expect(run([], set([SET_FILTER_BLANK]))).toBe(true)
      expect(run(['Owner'], set([SET_FILTER_BLANK]))).toBe(false)
      expect(run([], set(['Owner', SET_FILTER_BLANK]))).toBe(true)
    })

    it('delegates non-set descriptors to the joined value (Text Filter)', () => {
      // Arrange — "contains eng" over the joined "Owner, Engineer"
      const m = text('eng')

      // Act / Assert
      expect(run(['Owner', 'Engineer'], m)).toBe(true)
      expect(run(['Owner'], m)).toBe(false)
    })
  })

  describe('toDayKey', () => {
    it('normalizes assorted date shapes to YYYY-MM-DD', () => {
      // Act / Assert
      expect(toDayKey('2026-06-17')).toBe('2026-06-17')
      expect(toDayKey('2026-06-17T23:59:00')).toBe('2026-06-17')
      expect(toDayKey(new Date(2026, 5, 17))).toBe('2026-06-17')
    })

    it('returns null for empty or invalid values', () => {
      // Act / Assert
      expect(toDayKey(null)).toBeNull()
      expect(toDayKey('')).toBeNull()
      expect(toDayKey('not-a-date')).toBeNull()
    })
  })
})
