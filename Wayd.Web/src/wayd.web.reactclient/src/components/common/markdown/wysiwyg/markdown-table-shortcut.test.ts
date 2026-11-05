import type { Editor } from '@tiptap/core'
import {
  createTestEditor,
  placeCursorBefore,
  pressKey,
  stubEditorLayout,
  typeText,
} from './editor-test-utils'
import { parseTableHeaderRow } from './markdown-table-shortcut'

/** Puts the cursor at the end of the document's first textblock. */
const placeCursorAtEndOfFirstLine = (editor: Editor) => {
  const first = editor.state.doc.firstChild!
  editor.commands.setTextSelection(1 + first.content.size)
}

const hasTable = (editor: Editor) => {
  let found = false
  editor.state.doc.descendants((node) => {
    if (node.type.name === 'table') found = true
    return !found
  })
  return found
}

describe('parseTableHeaderRow', () => {
  it.each([
    ['| Name | Owner |', ['Name', 'Owner']],
    ['  |a|b|c|  ', ['a', 'b', 'c']],
    ['| a \\| b | c |', ['a | b', 'c']],
    ['| Name | |', ['Name', '']],
  ])('reads %p as cells %p', (text, cells) => {
    // Arrange / Act / Assert
    expect(parseTableHeaderRow(text)).toEqual(cells)
  })

  it.each(['| one |', 'a | b', '| a | b', '||', '| a | b \\|', 'plain'])(
    'refuses %p',
    (text) => {
      // Arrange / Act / Assert
      expect(parseTableHeaderRow(text)).toBeNull()
    },
  )
})

describe('MarkdownTableShortcut', () => {
  let editor: Editor | undefined

  beforeAll(stubEditorLayout)

  afterEach(() => {
    editor?.destroy()
    editor = undefined
  })

  it('turns a typed header row into a table on Enter', () => {
    // Arrange
    editor = createTestEditor('| Name | Owner |')
    placeCursorAtEndOfFirstLine(editor)

    // Act
    pressKey(editor, 'Enter')

    // Assert
    expect(hasTable(editor)).toBe(true)
    expect(editor.getMarkdown()).toContain('| Name')
    expect(editor.getMarkdown()).toContain('Owner |')
  })

  it('puts the cursor in the first cell of the new row', () => {
    // Arrange
    editor = createTestEditor('| Name | Owner |')
    placeCursorAtEndOfFirstLine(editor)

    // Act
    pressKey(editor, 'Enter')
    typeText(editor, 'Ada')

    // Assert
    const table = editor.state.doc.firstChild!
    expect(table.child(1).child(0).textContent).toBe('Ada')
  })

  it('splits the line as usual when the cursor is not at its end', () => {
    // Arrange
    editor = createTestEditor('| Name | Owner |')
    placeCursorBefore(editor, 'Owner')

    // Act
    pressKey(editor, 'Enter')

    // Assert
    expect(hasTable(editor)).toBe(false)
  })

  it('keeps Enter usual for a line that is not a header row', () => {
    // Arrange
    editor = createTestEditor('a | b')
    placeCursorAtEndOfFirstLine(editor)

    // Act
    pressKey(editor, 'Enter')

    // Assert
    expect(hasTable(editor)).toBe(false)
  })

  it('does not put a table inside a table cell', () => {
    // Arrange
    editor = createTestEditor('| A | B |\n| --- | --- |\n| x | y |')
    placeCursorBefore(editor, 'x')
    typeText(editor, '| c | d |')

    // Act
    pressKey(editor, 'Enter')

    // Assert
    const tables: string[] = []
    editor.state.doc.descendants((node) => {
      if (node.type.name === 'table') tables.push(node.type.name)
    })
    expect(tables).toHaveLength(1)
  })
})
