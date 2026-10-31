import type { Editor } from '@tiptap/core'
import {
  createTestEditor,
  stubEditorLayout,
  typeText,
} from './editor-test-utils'

const images = (editor: Editor) => {
  const found: Record<string, unknown>[] = []
  editor.state.doc.descendants((node) => {
    if (node.type.name === 'image') found.push(node.attrs)
  })
  return found
}

describe('markdownImageInputRule', () => {
  let editor: Editor | undefined

  beforeAll(stubEditorLayout)

  afterEach(() => {
    editor?.destroy()
    editor = undefined
  })

  it('makes an image when "![alt](address)" is closed, without a stray ")"', () => {
    // Arrange
    editor = createTestEditor('')

    // Act
    typeText(editor, 'Logo ![a](https://x.dev/a.png)')

    // Assert
    expect(images(editor)).toEqual([
      expect.objectContaining({ src: 'https://x.dev/a.png', alt: 'a' }),
    ])
    expect(editor.getMarkdown().trimEnd()).toBe(
      'Logo ![a](https://x.dev/a.png)',
    )
  })

  it('leaves an address the sanitizer would refuse as text', () => {
    // Arrange
    editor = createTestEditor('')

    // Act
    typeText(editor, '![a](javascript:alert)')

    // Assert
    expect(images(editor)).toEqual([])
    expect(editor.getText()).toBe('![a](javascript:alert)')
  })
})
