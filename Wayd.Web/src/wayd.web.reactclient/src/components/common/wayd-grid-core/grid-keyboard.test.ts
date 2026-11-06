import { moveCell, withPinnedIndex } from './grid-keyboard'

const BOUNDS = { rowCount: 50, colCount: 4, pageSize: 10 }

describe('moveCell', () => {
  it.each([
    ['ArrowUp', { rowIndex: 4, colIndex: 2 }],
    ['ArrowDown', { rowIndex: 6, colIndex: 2 }],
    ['ArrowLeft', { rowIndex: 5, colIndex: 1 }],
    ['ArrowRight', { rowIndex: 5, colIndex: 3 }],
    ['Home', { rowIndex: 5, colIndex: 0 }],
    ['End', { rowIndex: 5, colIndex: 3 }],
    ['PageUp', { rowIndex: 0, colIndex: 2 }],
    ['PageDown', { rowIndex: 15, colIndex: 2 }],
  ])('moves %s from the middle of the grid', (key, expected) => {
    // Arrange
    const from = { rowIndex: 5, colIndex: 2 }

    // Act
    const next = moveCell(key, from, BOUNDS)

    // Assert
    expect(next).toEqual(expected)
  })

  it.each([
    ['ArrowUp', { rowIndex: 0, colIndex: 0 }, { rowIndex: 0, colIndex: 0 }],
    ['ArrowLeft', { rowIndex: 0, colIndex: 0 }, { rowIndex: 0, colIndex: 0 }],
    ['ArrowDown', { rowIndex: 49, colIndex: 3 }, { rowIndex: 49, colIndex: 3 }],
    [
      'ArrowRight',
      { rowIndex: 49, colIndex: 3 },
      { rowIndex: 49, colIndex: 3 },
    ],
    ['PageDown', { rowIndex: 45, colIndex: 1 }, { rowIndex: 49, colIndex: 1 }],
    ['PageUp', { rowIndex: 3, colIndex: 1 }, { rowIndex: 0, colIndex: 1 }],
  ])('stops %s at the edge instead of wrapping', (key, from, expected) => {
    // Arrange / Act
    const next = moveCell(key, from, BOUNDS)

    // Assert
    expect(next).toEqual(expected)
  })

  it.each([
    ['Home', { ctrlKey: true }, { rowIndex: 0, colIndex: 0 }],
    ['End', { ctrlKey: true }, { rowIndex: 49, colIndex: 3 }],
    ['Home', { metaKey: true }, { rowIndex: 0, colIndex: 0 }],
    ['End', { metaKey: true }, { rowIndex: 49, colIndex: 3 }],
  ])(
    'moves Ctrl/Cmd+%s to the corner of the grid',
    (key, modifiers, expected) => {
      // Arrange
      const from = { rowIndex: 20, colIndex: 1 }

      // Act
      const next = moveCell(key, from, BOUNDS, modifiers)

      // Assert
      expect(next).toEqual(expected)
    },
  )

  it('moves at least one row per page when nothing fits the viewport', () => {
    // Arrange — a viewport too short to hold a row reports a page of 0
    const from = { rowIndex: 5, colIndex: 0 }

    // Act
    const next = moveCell('PageDown', from, { ...BOUNDS, pageSize: 0 })

    // Assert
    expect(next).toEqual({ rowIndex: 6, colIndex: 0 })
  })

  it('returns null for keys that do not move the cursor', () => {
    // Arrange / Act
    const next = moveCell('a', { rowIndex: 5, colIndex: 2 }, BOUNDS)

    // Assert
    expect(next).toBeNull()
  })

  it('returns null in an empty grid', () => {
    // Arrange / Act
    const next = moveCell(
      'ArrowDown',
      { rowIndex: 0, colIndex: 0 },
      { rowCount: 0, colCount: 4, pageSize: 10 },
    )

    // Assert
    expect(next).toBeNull()
  })
})

describe('withPinnedIndex', () => {
  it('adds a pinned index outside the window, keeping index order', () => {
    // Arrange / Act
    const indexes = withPinnedIndex([40, 41, 42], 3)

    // Assert
    expect(indexes).toEqual([3, 40, 41, 42])
  })

  it('leaves the window alone when the pinned index is already in it', () => {
    // Arrange / Act
    const indexes = withPinnedIndex([40, 41, 42], 41)

    // Assert
    expect(indexes).toEqual([40, 41, 42])
  })

  it.each([null, -1])(
    'leaves the window alone with no pinned index (%p)',
    (pinned) => {
      // Arrange / Act
      const indexes = withPinnedIndex([0, 1, 2], pinned)

      // Assert
      expect(indexes).toEqual([0, 1, 2])
    },
  )
})
