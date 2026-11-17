import { SET_FILTER_BLANK } from '../filters/filter-model'
import type { ColumnFilterModel } from '../filters/filter-model'
import {
  escapeGridifyValue,
  toGridifyColumnFilter,
  toGridifyFilter,
  toGridifySort,
} from './gridify-query'
import { asCalendarDate } from '@/src/utils/calendar-date'
import { asInstant } from '@/src/utils/instant'

const text = (
  op: string,
  value = '',
  join: 'AND' | 'OR' = 'AND',
): ColumnFilterModel =>
  ({ type: 'text', conditions: [{ op, value }], join }) as ColumnFilterModel

describe('gridify-query', () => {
  describe('escapeGridifyValue', () => {
    it('leaves letters, digits and spaces alone', () => {
      expect(escapeGridifyValue('Vendor review 2026 é')).toBe(
        'Vendor review 2026 é',
      )
    })

    it('escapes separators, operators, a backslash and the /i suffix', () => {
      expect(escapeGridifyValue('a,b|c(d)\\e*=^<$/i%')).toBe(
        'a\\,b\\|c\\(d\\)\\\\e\\*\\=\\^\\<\\$\\/i\\%',
      )
    })
  })

  describe('toGridifyColumnFilter — text', () => {
    it.each([
      ['contains', 'name=*vend'],
      ['notContains', 'name!*vend'],
      ['equals', 'name=vend'],
      ['notEqual', 'name!=vend'],
      ['startsWith', 'name^vend'],
      ['endsWith', 'name$vend'],
    ])('%s', (op, expected) => {
      expect(toGridifyColumnFilter('name', text(op, ' vend '))).toBe(expected)
    })

    it('sends blank and not blank with no value', () => {
      expect(toGridifyColumnFilter('name', text('blank'))).toBe('name=')
      expect(toGridifyColumnFilter('name', text('notBlank'))).toBe('name!=')
    })

    it('escapes the value', () => {
      expect(toGridifyColumnFilter('name', text('equals', '*a,b'))).toBe(
        'name=\\*a\\,b',
      )
    })

    it('filters nothing while the value is empty', () => {
      expect(toGridifyColumnFilter('name', text('contains', '  '))).toBeNull()
    })

    it('groups several conditions with their join', () => {
      const model: ColumnFilterModel = {
        type: 'text',
        conditions: [
          { op: 'contains', value: 'a' },
          { op: 'endsWith', value: 'z' },
        ],
        join: 'OR',
      }
      expect(toGridifyColumnFilter('name', model)).toBe('(name=*a|name$z)')
    })
  })

  describe('toGridifyColumnFilter — number', () => {
    it.each([
      ['equals', 'score=5'],
      ['notEqual', 'score!=5'],
      ['greaterThan', 'score>5'],
      ['greaterThanOrEqual', 'score>=5'],
      ['lessThan', 'score<5'],
      ['lessThanOrEqual', 'score<=5'],
    ])('%s', (op, expected) => {
      const model = {
        type: 'number',
        conditions: [{ op, value: 5 }],
        join: 'AND',
      } as ColumnFilterModel
      expect(toGridifyColumnFilter('score', model)).toBe(expected)
    })

    it('sends a range inclusively, whichever way round it was entered', () => {
      const model: ColumnFilterModel = {
        type: 'number',
        conditions: [{ op: 'inRange', value: 20, valueTo: 10 }],
        join: 'AND',
      }
      expect(toGridifyColumnFilter('score', model)).toBe(
        '(score>=10,score<=20)',
      )
    })
  })

  describe('toGridifyColumnFilter — date', () => {
    const date = (op: string, value: string, valueTo?: string) =>
      ({
        type: 'date',
        conditions: [{ op, value, valueTo }],
        join: 'AND',
      }) as ColumnFilterModel

    it('compares calendar dates directly', () => {
      expect(
        toGridifyColumnFilter(
          'due',
          date('equals', asCalendarDate('2026-03-01')),
        ),
      ).toBe('due=2026-03-01')
      expect(
        toGridifyColumnFilter(
          'due',
          date('before', asCalendarDate('2026-03-01')),
        ),
      ).toBe('due<2026-03-01')
      expect(
        toGridifyColumnFilter(
          'due',
          date('after', asCalendarDate('2026-03-01')),
        ),
      ).toBe('due>2026-03-01')
      expect(
        toGridifyColumnFilter(
          'due',
          date(
            'inRange',
            asCalendarDate('2026-03-09'),
            asCalendarDate('2026-03-01'),
          ),
        ),
      ).toBe('(due>=2026-03-01,due<=2026-03-09)')
    })
  })

  describe('toGridifyColumnFilter — dateTime', () => {
    const dateTime = (op: string, value: string, valueTo?: string) =>
      ({
        type: 'dateTime',
        conditions: [{ op, value, valueTo }],
        join: 'AND',
      }) as ColumnFilterModel

    it('compares to the minute, as instants', () => {
      expect(
        toGridifyColumnFilter(
          'at',
          dateTime('equals', asInstant('2026-03-01T09:30:45Z')),
        ),
      ).toBe('(at>=2026-03-01T09:30:00.000Z,at<2026-03-01T09:31:00.000Z)')
      expect(
        toGridifyColumnFilter(
          'at',
          dateTime('notEqual', asInstant('2026-03-01T09:30:00Z')),
        ),
      ).toBe('(at<2026-03-01T09:30:00.000Z|at>=2026-03-01T09:31:00.000Z)')
      expect(
        toGridifyColumnFilter(
          'at',
          dateTime('after', asInstant('2026-03-01T09:30:00Z')),
        ),
      ).toBe('at>=2026-03-01T09:31:00.000Z')
      expect(
        toGridifyColumnFilter(
          'at',
          dateTime(
            'inRange',
            asInstant('2026-03-02T00:00:00Z'),
            asInstant('2026-03-01T00:00:00Z'),
          ),
        ),
      ).toBe('(at>=2026-03-01T00:00:00.000Z,at<2026-03-02T00:01:00.000Z)')
    })
  })

  describe('toGridifyColumnFilter — set', () => {
    it('ORs the chosen values, a blank as an empty value', () => {
      const model: ColumnFilterModel = {
        type: 'set',
        values: ['Failed', SET_FILTER_BLANK, 'In (progress)'],
      }
      expect(toGridifyColumnFilter('status', model)).toBe(
        '(status=Failed|status=|status=In \\(progress\\))',
      )
    })

    it('filters nothing with no values chosen', () => {
      expect(
        toGridifyColumnFilter('status', { type: 'set', values: [] }),
      ).toBeNull()
    })
  })

  describe('toGridifyFilter', () => {
    it('ANDs the columns and leaves out the ones that filter nothing', () => {
      expect(
        toGridifyFilter([
          { id: 'status', value: { type: 'set', values: ['A', 'B'] } },
          { id: 'name', value: text('contains', '') },
          { id: 'error', value: text('contains', 'dup') },
        ]),
      ).toBe('(status=A|status=B),error=*dup')
    })

    it('is undefined when nothing filters', () => {
      expect(toGridifyFilter([])).toBeUndefined()
    })
  })

  describe('toGridifySort', () => {
    it('lists every sorted column in order', () => {
      expect(
        toGridifySort([
          { id: 'status', desc: false },
          { id: 'due', desc: true },
        ]),
      ).toBe('status asc, due desc')
    })

    it('is undefined with no sort', () => {
      expect(toGridifySort([])).toBeUndefined()
    })
  })
})
