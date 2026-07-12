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

  // Minimal Row stand-in: the accessor/cell only read `.original`.
  const rowOf = (original: Project): Row<Project> =>
    ({ original }) as unknown as Row<Project>

  const column = createCsvColumn<Project>({
    id: 'owners',
    header: 'Owners',
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

  it('marks the column for token faceting via meta.multiValueSplit', () => {
    // Arrange / Act — the grid uses this to build the set list from individual
    // tokens (round-trips the accessor's ", " join). No data/options are baked
    // into the column; options come from live faceting.
    expect(column.meta?.filterType).toBe('set')
    expect(column.meta?.filterOptions).toBeUndefined()

    // Assert — the split recovers the tokens from a joined accessor value.
    expect(column.meta?.multiValueSplit?.('Bob, Alice')).toEqual([
      'Bob',
      'Alice',
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
    expect(filterFn(rowOf({ owners: 'Bob, Alice' }), 'owners', model)).toBe(
      true,
    )
    expect(filterFn(rowOf({ owners: 'Carol' }), 'owners', model)).toBe(false)
  })

  it('supports an explicit getValues for array/object sources', () => {
    // Arrange
    interface PeopleRow {
      people: { name: string }[]
    }
    const col = createCsvColumn<PeopleRow>({
      id: 'people',
      header: 'People',
      getValues: (row) => row.people.map((p) => p.name),
    })

    // Act / Assert — accessor joins the extracted names
    const accessorFn = accessorOf<PeopleRow>(col)
    expect(accessorFn({ people: [{ name: 'Zoe' }, { name: 'Amy' }] })).toBe(
      'Zoe, Amy',
    )
    expect(col.meta?.multiValueSplit).toBe(column.meta?.multiValueSplit)
  })
})
