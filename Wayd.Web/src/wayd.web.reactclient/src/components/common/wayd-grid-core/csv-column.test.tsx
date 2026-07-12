import type { ColumnDef, Row } from '@tanstack/react-table'

import { createCsvColumn, splitCsv } from './csv-column'
import type { SetFilterModel } from './filters'

describe('splitCsv', () => {
  it('splits, trims, and drops empty tokens', () => {
    // Arrange / Act / Assert
    expect(splitCsv('Alice, Bob ,  , Carol')).toEqual(['Alice', 'Bob', 'Carol'])
  })

  it('returns an empty array for blank input', () => {
    // Arrange / Act / Assert
    expect(splitCsv(undefined)).toEqual([])
    expect(splitCsv('')).toEqual([])
    expect(splitCsv('  ')).toEqual([])
  })
})

describe('createCsvColumn', () => {
  interface Project {
    owners: string
  }

  const data: Project[] = [
    { owners: 'Bob, Alice' },
    { owners: 'alice, Carol' },
    { owners: '' },
  ]

  // Minimal Row stand-in: the accessor/cell only read `.original`.
  const rowOf = (original: Project): Row<Project> =>
    ({ original }) as unknown as Row<Project>

  const column = createCsvColumn<Project>({
    id: 'owners',
    header: 'Owners',
    data,
  })

  // TanStack's ColumnDef is a union that only exposes accessorFn on the
  // accessor variant; the factory always sets one, so read it via a cast.
  const accessorOf = <R,>(col: ColumnDef<R, any>): ((row: R) => string) =>
    (col as unknown as { accessorFn: (row: R) => string }).accessorFn

  it('joins the row values for the accessor (search/sort/export)', () => {
    // Arrange
    const accessorFn = accessorOf<Project>(column)

    // Act / Assert — splitCsv default reads row[id]
    expect(accessorFn({ owners: 'Bob, Alice' })).toBe('Bob, Alice')
  })

  it('derives distinct filter options from individual values, case-insensitively sorted', () => {
    // Arrange / Act
    const options = column.meta?.filterOptions

    // Assert — 'Alice' and 'alice' are distinct values but sort adjacent
    // (case-insensitive compare is stable, so first-seen 'Alice' leads);
    // both are offered, whole CSV combos are not.
    expect(options).toEqual([
      { label: 'Alice', value: 'Alice' },
      { label: 'alice', value: 'alice' },
      { label: 'Bob', value: 'Bob' },
      { label: 'Carol', value: 'Carol' },
    ])
  })

  it('wires a multi-value set filter that matches on ANY shared value', () => {
    // Arrange
    const filterFn = column.filterFn as (
      row: Row<Project>,
      columnId: string,
      value: unknown,
    ) => boolean
    const model: SetFilterModel = { type: 'set', values: ['Bob'] }

    // Act / Assert — a row with several owners still matches on one of them
    expect(filterFn(rowOf({ owners: 'Bob, Alice' }), 'owners', model)).toBe(true)
    expect(filterFn(rowOf({ owners: 'Carol' }), 'owners', model)).toBe(false)
  })

  it('supports an explicit getValues for array/object sources', () => {
    // Arrange
    interface Row {
      people: { name: string }[]
    }
    const col = createCsvColumn<Row>({
      id: 'people',
      header: 'People',
      data: [{ people: [{ name: 'Zoe' }, { name: 'Amy' }] }],
      getValues: (row) => row.people.map((p) => p.name),
    })

    // Act / Assert
    expect(col.meta?.filterOptions).toEqual([
      { label: 'Amy', value: 'Amy' },
      { label: 'Zoe', value: 'Zoe' },
    ])
    const accessorFn = accessorOf<Row>(col)
    expect(accessorFn({ people: [{ name: 'Zoe' }, { name: 'Amy' }] })).toBe(
      'Zoe, Amy',
    )
  })
})
