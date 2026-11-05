import type { Editor } from '@tiptap/core'
import {
  createTestEditor,
  stubEditorLayout,
  typeText,
} from './editor-test-utils'

/** The hrefs of every link in the document, in order. */
const linkHrefs = (editor: Editor) => {
  const hrefs: string[] = []
  editor.state.doc.descendants((node) => {
    const link = node.marks.find((mark) => mark.type.name === 'link')
    if (link) hrefs.push(link.attrs.href)
  })
  return hrefs
}

describe('markdownLinkInputRule', () => {
  let editor: Editor | undefined

  beforeAll(stubEditorLayout)

  afterEach(() => {
    editor?.destroy()
    editor = undefined
  })

  it('makes a link when "[text](address)" is closed', () => {
    // Arrange
    editor = createTestEditor('')

    // Act
    typeText(editor, 'See [Wayd](https://wayd.dev)')

    // Assert
    expect(linkHrefs(editor)).toEqual(['https://wayd.dev'])
    expect(editor.getMarkdown().trimEnd()).toBe('See [Wayd](https://wayd.dev)')
  })

  it('adds https:// to a bare host, as the link dialog does', () => {
    // Arrange
    editor = createTestEditor('')

    // Act
    typeText(editor, '[docs](wayd.dev/guide)')

    // Assert
    expect(linkHrefs(editor)).toEqual(['https://wayd.dev/guide'])
  })

  it('continues text typed after the link outside it', () => {
    // Arrange
    editor = createTestEditor('')

    // Act
    typeText(editor, '[a](https://x.dev) and more')

    // Assert
    expect(editor.getMarkdown().trimEnd()).toBe('[a](https://x.dev) and more')
  })

  it('leaves an address the sanitizer would refuse as text', () => {
    // Arrange
    editor = createTestEditor('')

    // Act
    typeText(editor, '[x](javascript:alert)')

    // Assert
    expect(linkHrefs(editor)).toEqual([])
    expect(editor.getText()).toBe('[x](javascript:alert)')
  })

  it.each([
    ['an image', '![alt](https://x.dev/a.png)'],
    ['an escaped bracket', '\\[x](https://x.dev)'],
  ])('leaves %s as text', (_, typed) => {
    // Arrange
    editor = createTestEditor('')

    // Act
    typeText(editor, typed)

    // Assert
    expect(linkHrefs(editor)).toEqual([])
  })
})
