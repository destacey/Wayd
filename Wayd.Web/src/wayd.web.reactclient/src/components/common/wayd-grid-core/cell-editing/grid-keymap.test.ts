import { gridHintsFor, gridKeymapFor } from './grid-keymap'

const titles = (options: Parameters<typeof gridKeymapFor>[0]) =>
  gridKeymapFor(options).map((section) => section.title)

const rowKeys = (rows: Parameters<typeof gridKeymapFor>[0]['rows']) =>
  gridKeymapFor({ editing: true, tree: true, rows })
    .find((section) => section.title === 'Rows')
    ?.bindings.map((binding) => binding.keys)

describe('gridKeymapFor', () => {
  it('lists the editing and tree sections only for grids that have them', () => {
    // Arrange / Act
    const plain = titles({ editing: false, tree: false })
    const full = titles({ editing: true, tree: true })

    // Assert
    expect(plain).toStrictEqual(['Moving Around'])
    expect(full).toStrictEqual(['Moving Around', 'Tree', 'Editing'])
  })

  it('lists only the row actions the grid supports', () => {
    // Arrange / Act
    const keys = rowKeys({ delete: true, open: false })

    // Assert
    expect(keys).toStrictEqual(['Ctrl + Delete'])
  })

  it('lists every row action the grid supports, in order', () => {
    // Arrange / Act
    const keys = rowKeys({ insert: true, move: true, delete: true, open: true })

    // Assert
    expect(keys).toStrictEqual([
      'Insert / Ctrl + Enter',
      'Alt + Shift + Right / Left',
      'Ctrl + Delete',
      'Ctrl + Shift + Enter',
    ])
  })

  it('lists no Rows section when the grid supports none', () => {
    // Arrange / Act
    const keys = rowKeys({})

    // Assert
    expect(keys).toBeUndefined()
  })
})

describe('gridHintsFor', () => {
  const labels = (options: Parameters<typeof gridHintsFor>[0]) =>
    gridHintsFor(options).map((hint) => `${hint.keys} ${hint.label}`)

  it('offers editing on a cell that edits, with the row keys the grid supports', () => {
    // Arrange / Act
    const hints = labels({
      mode: 'cell',
      cellEditable: true,
      rows: { insert: true, delete: true },
    })

    // Assert
    expect(hints).toStrictEqual([
      'Enter edit',
      'Arrows move',
      'Insert add row',
      'Ctrl + Delete delete',
      'Tab leave grid',
    ])
  })

  it('leaves editing out on a cell that does not edit', () => {
    // Arrange / Act
    const hints = labels({ mode: 'cell', cellEditable: false, rows: {} })

    // Assert
    expect(hints).toStrictEqual(['Arrows move', 'Tab leave grid'])
  })

  it('lists every row key, in order, when the grid has them all', () => {
    // Arrange / Act
    const hints = labels({
      mode: 'cell',
      cellEditable: true,
      rows: { insert: true, move: true, delete: true, open: true },
    })

    // Assert
    expect(hints).toStrictEqual([
      'Enter edit',
      'Arrows move',
      'Insert add row',
      'Alt + Shift + ← / → outdent / indent',
      'Ctrl + Delete delete',
      'Ctrl + Shift + Enter open',
      'Tab leave grid',
    ])
  })

  it('shows the saving keys in a text editor', () => {
    // Arrange / Act / Assert
    expect(labels({ mode: 'text' })).toStrictEqual([
      'Enter save & down',
      'Tab save & next',
      'Esc undo',
    ])
  })

  it('shows the list keys in a single-choice list', () => {
    // Arrange / Act / Assert
    expect(labels({ mode: 'select' })).toStrictEqual([
      '↑ / ↓ choose',
      'Enter pick',
      'Tab next cell',
      'Esc close list',
    ])
  })

  it('shows the list keys in a multiple-choice list', () => {
    // Arrange / Act / Assert
    expect(labels({ mode: 'multiselect' })).toStrictEqual([
      '↑ / ↓ choose',
      'Enter add / remove',
      'Tab save & next',
      'Esc close list',
    ])
  })

  it('shows the calendar key in a date editor', () => {
    // Arrange / Act / Assert
    expect(labels({ mode: 'date' })).toStrictEqual([
      '↓ calendar',
      'Enter save & down',
      'Tab save & next',
      'Esc undo',
    ])
  })
})
