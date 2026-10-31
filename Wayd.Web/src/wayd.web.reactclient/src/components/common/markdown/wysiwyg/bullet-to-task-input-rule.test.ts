import type { Editor } from '@tiptap/core'
import {
  createTestEditor,
  placeCursorBefore,
  stubEditorLayout,
  typeText,
} from './editor-test-utils'

/** The types of the document's top-level nodes, without the empty paragraph the editor keeps last. */
const topLevelTypes = (editor: Editor) =>
  editor
    .getJSON()
    .content?.filter((node) => node.type !== 'paragraph' || node.content)
    .map((node) => node.type)

describe('bulletToTaskInputRule', () => {
  let editor: Editor | undefined

  beforeAll(stubEditorLayout)

  afterEach(() => {
    editor?.destroy()
    editor = undefined
  })

  it('turns a bullet into an open task when its line starts with "[ ] "', () => {
    // Arrange
    editor = createTestEditor('- Test')
    placeCursorBefore(editor, 'Test')

    // Act
    typeText(editor, '[ ] ')

    // Assert
    expect(editor.getMarkdown().trimEnd()).toBe('- [ ] Test')
  })

  it('turns a bullet into a done task when its line starts with "[x] "', () => {
    // Arrange
    editor = createTestEditor('- Test')
    placeCursorBefore(editor, 'Test')

    // Act
    typeText(editor, '[x] ')

    // Assert
    expect(editor.getMarkdown().trimEnd()).toBe('- [x] Test')
  })

  it('leaves the cursor at the start of the task text', () => {
    // Arrange
    editor = createTestEditor('- Test')
    placeCursorBefore(editor, 'Test')

    // Act
    typeText(editor, '[ ] ')
    typeText(editor, 'A ')

    // Assert
    expect(editor.getMarkdown().trimEnd()).toBe('- [ ] A Test')
  })

  it('converts only that item, keeping the bullets around it', () => {
    // Arrange
    editor = createTestEditor('- one\n- two\n- three')
    placeCursorBefore(editor, 'two')

    // Act
    typeText(editor, '[ ] ')

    // Assert
    expect(topLevelTypes(editor)).toEqual([
      'bulletList',
      'taskList',
      'bulletList',
    ])
    expect(editor.getText()).toContain('one')
    expect(editor.getText()).toContain('three')
  })

  it('keeps a nested bullet at its depth', () => {
    // Arrange
    editor = createTestEditor('- parent\n  - child')
    placeCursorBefore(editor, 'child')

    // Act
    typeText(editor, '[ ] ')

    // Assert
    const parentItem = editor.state.doc.child(0).child(0)
    expect(parentItem.type.name).toBe('listItem')
    expect(parentItem.child(1).type.name).toBe('taskList')
  })

  it('keeps the brackets as text when typed after the start of the bullet', () => {
    // Arrange
    editor = createTestEditor('- Test')
    placeCursorBefore(editor, 'est')

    // Act
    typeText(editor, '[ ] ')

    // Assert
    expect(topLevelTypes(editor)).toEqual(['bulletList'])
  })

  it('leaves the existing rule to turn a plain line into a task', () => {
    // Arrange
    editor = createTestEditor('Test')
    placeCursorBefore(editor, 'Test')

    // Act
    typeText(editor, '[ ] ')

    // Assert
    expect(editor.getMarkdown().trimEnd()).toBe('- [ ] Test')
  })
})
