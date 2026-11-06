import {
  applyEntries,
  cellKey,
  defaultEquals,
  mapServerErrors,
  nextEditableCell,
  type CellEntry,
  type ServerErrorTarget,
} from './edit-state'

interface Node {
  id: string
  name: string
  children: Node[]
}

const node = (id: string, children: Node[] = []): Node => ({
  id,
  name: `n${id}`,
  children,
})

const nameEntry = (
  rowKey: string,
  value: string,
  phase: CellEntry['phase'] = 'saving',
): CellEntry => ({
  rowKey,
  field: 'name',
  columnId: 'name',
  phase,
  value,
  hasValue: true,
  seq: 1,
  apply: (row, name) => ({ ...row, name }),
})

const keyOf = (row: Node) => row.id

describe('applyEntries', () => {
  it('returns the same rows when nothing is held', () => {
    // Arrange
    const data = [node('1'), node('2')]

    // Act
    const result = applyEntries(data, {}, keyOf, false)

    // Assert
    expect(result).toBe(data)
  })

  it('applies a held value to its row and keeps the other rows', () => {
    // Arrange
    const data = [node('1'), node('2')]

    // Act
    const result = applyEntries(
      data,
      { [cellKey('2', 'name')]: nameEntry('2', 'Renamed') },
      keyOf,
      false,
    )

    // Assert
    expect(result.map((row) => row.name)).toEqual(['n1', 'Renamed'])
    expect(result[0]).toBe(data[0])
  })

  it('reaches a nested row and rebuilds only its ancestors', () => {
    // Arrange
    const data = [node('1', [node('1.1'), node('1.2')]), node('2')]

    // Act
    const result = applyEntries(
      data,
      { [cellKey('1.2', 'name')]: nameEntry('1.2', 'Deep') },
      keyOf,
      true,
    )

    // Assert
    expect(result[0].children[1].name).toBe('Deep')
    expect(result[0]).not.toBe(data[0])
    expect(result[0].children[0]).toBe(data[0].children[0])
    expect(result[1]).toBe(data[1])
  })

  it('shows nothing for an error mark without a value', () => {
    // Arrange
    const data = [node('1')]
    const mark: CellEntry = {
      ...nameEntry('1', 'x', 'error'),
      hasValue: false,
      value: undefined,
    }

    // Act
    const result = applyEntries(
      data,
      { [cellKey('1', 'name')]: mark },
      keyOf,
      false,
    )

    // Assert
    expect(result).toBe(data)
  })
})

describe('defaultEquals', () => {
  it.each([
    [['a', 'b'], ['a', 'b'], true],
    [['a', 'b'], ['b', 'a'], false],
    [null, '', true],
    [undefined, null, true],
    ['x', 'x', true],
    ['x', 'y', false],
    [0, null, false],
  ])('%p vs %p is %p', (a, b, expected) => {
    // Arrange — the pair comes from the table

    // Act
    const result = defaultEquals(a, b)

    // Assert
    expect(result).toBe(expected)
  })
})

describe('mapServerErrors', () => {
  const name: ServerErrorTarget = { columnId: 'name', label: 'Name' }
  const owner: ServerErrorTarget = { columnId: 'owner', label: 'Owner' }
  const targets = {
    byField: new Map([['name', name]]),
    byColumn: new Map([
      ['name', name],
      ['owner', owner],
    ]),
    fieldToColumn: { assigneeIds: 'owner' },
  }

  it('places field errors on columns by their label, ignoring casing', () => {
    // Arrange
    const error = {
      status: 422,
      errors: { Name: ['Too long.'], AssigneeIds: ['Gone.'] },
    }

    // Act
    const result = mapServerErrors(error, targets)

    // Assert
    expect(result.byColumn).toEqual({
      name: 'Name: Too long.',
      owner: 'Owner: Gone.',
    })
    expect(result.unmatched).toEqual([])
  })

  it('reports keys no column claims as unmatched', () => {
    // Arrange
    const error = { status: 422, errors: { Budget: ['Over.'] } }

    // Act
    const result = mapServerErrors(error, targets)

    // Assert
    expect(result.byColumn).toEqual({})
    expect(result.unmatched).toEqual(['Over.'])
  })

  it('reports the detail of a response without field errors', () => {
    // Arrange
    const error = { status: 400, detail: 'Locked.' }

    // Act
    const result = mapServerErrors(error, targets)

    // Assert
    expect(result.unmatched).toEqual(['Locked.'])
  })
})

describe('nextEditableCell', () => {
  // 3 rows × 3 columns; column 0 is not editable.
  const editable = (_row: number, col: number) => col !== 0
  const size = { rowCount: 3, colCount: 3 }

  it.each([
    ['moves right within the row', [0, 1], 1, [0, 2]],
    [
      'wraps to the next row, skipping cells that do not edit',
      [0, 2],
      1,
      [1, 1],
    ],
    ['wraps backwards to the previous row', [1, 1], -1, [0, 2]],
    ['returns null past the last editable cell', [2, 2], 1, null],
  ] as const)('%s', (_name, [rowIndex, colIndex], direction, expected) => {
    // Arrange
    const from = { rowIndex, colIndex }

    // Act
    const result = nextEditableCell(from, direction, size, editable)

    // Assert
    expect(result).toEqual(
      expected && { rowIndex: expected[0], colIndex: expected[1] },
    )
  })
})
