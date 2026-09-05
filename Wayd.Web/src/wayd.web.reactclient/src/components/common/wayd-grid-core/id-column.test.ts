import type { ColumnDef } from './index'
import {
  ID_COLUMN_ID,
  createIdColumn,
  hasIdColumn,
  rowsHaveId,
  withIdColumn,
} from './id-column'

interface Row {
  id: string
  name: string
}

const columns: ColumnDef<Row, any>[] = [{ accessorKey: 'name', header: 'Name' }]
const rows: Row[] = [{ id: 'a1b2', name: 'Payments Platform' }]

describe('createIdColumn', () => {
  it('starts hidden but stays in the chooser', () => {
    // Arrange / Act
    const column = createIdColumn<Row>()

    // Assert
    expect(column.meta?.hiddenByDefault).toBe(true)
    expect(column.meta?.unavailable).toBeUndefined()
  })

  it('opts out of the quick search so it cannot match invisibly', () => {
    // Arrange / Act
    const column = createIdColumn<Row>()

    // Assert
    expect((column as { enableGlobalFilter?: boolean }).enableGlobalFilter).toBe(
      false,
    )
  })
})

describe('rowsHaveId', () => {
  it.each([
    ['undefined data', undefined, false],
    ['empty data', [], false],
    ['string id', [{ id: 'a1b2', name: 'x' }], true],
    ['numeric id', [{ id: 7, name: 'x' }], true],
    ['empty string id', [{ id: '', name: 'x' }], false],
    ['no id property', [{ name: 'x' }], false],
    ['null id', [{ id: null, name: 'x' }], false],
  ])('is %s → %s', (_label, data, expected) => {
    // Arrange / Act
    const result = rowsHaveId(data as unknown[] | undefined)

    // Assert
    expect(result).toBe(expected)
  })

  it('skips null rows to find the first real one', () => {
    // Arrange
    const data = [null, { id: 'a1b2', name: 'x' }]

    // Act
    const result = rowsHaveId(data as unknown[])

    // Assert
    expect(result).toBe(true)
  })
})

describe('hasIdColumn', () => {
  it('finds a column declared by accessorKey', () => {
    // Arrange
    const defs: ColumnDef<Row, any>[] = [{ accessorKey: 'id', header: 'Id' }]

    // Act / Assert
    expect(hasIdColumn(defs)).toBe(true)
  })

  it('finds a column nested inside a band', () => {
    // Arrange
    const defs: ColumnDef<Row, any>[] = [
      { header: 'Meta', columns: [{ id: 'id', header: 'Id' }] } as ColumnDef<
        Row,
        any
      >,
    ]

    // Act / Assert
    expect(hasIdColumn(defs)).toBe(true)
  })

  it('is false when no column claims id', () => {
    // Arrange / Act / Assert
    expect(hasIdColumn(columns)).toBe(false)
  })
})

describe('withIdColumn', () => {
  it('appends the id column last', () => {
    // Arrange / Act
    const result = withIdColumn(columns, rows, true)

    // Assert
    expect(result).toHaveLength(2)
    expect(result[result.length - 1].id).toBe(ID_COLUMN_ID)
  })

  it('returns the same array when disabled, so column identity is stable', () => {
    // Arrange / Act
    const result = withIdColumn(columns, rows, false)

    // Assert
    expect(result).toBe(columns)
  })

  it('returns the same array when the rows carry no id', () => {
    // Arrange
    const idless = [{ name: 'Payments Platform' }]

    // Act
    const result = withIdColumn(columns as ColumnDef<any, any>[], idless, true)

    // Assert
    expect(result).toBe(columns)
  })

  it('leaves a consumer-defined id column alone', () => {
    // Arrange
    const defs: ColumnDef<Row, any>[] = [
      { accessorKey: 'id', header: 'Job Id' },
      { accessorKey: 'name', header: 'Name' },
    ]

    // Act
    const result = withIdColumn(defs, rows, true)

    // Assert
    expect(result).toBe(defs)
  })
})
