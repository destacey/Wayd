import { Editor, type JSONContent } from '@tiptap/core'
import { markdownEditorExtensions } from './markdown-extensions'

/** Serializes `paragraph` and parses the markdown back, returning the paragraph it becomes. */
const roundTripParagraph = (paragraph: JSONContent) => {
  const editor = new Editor({
    element: null,
    extensions: markdownEditorExtensions(),
  })
  editor.commands.setContent({ type: 'doc', content: [paragraph] })
  const markdown = editor.getMarkdown()
  editor.commands.setContent(markdown, { contentType: 'markdown' })
  const reparsed: JSONContent | undefined = editor.getJSON().content?.[0]
  editor.destroy()
  return { markdown, reparsed }
}

describe('markdownEditorExtensions', () => {
  it.each([
    ['a trailing backslash', 'C:\\temp\\'],
    ['a quote', 'the "best" page'],
    ['a backslash before a quote', 'a\\"b'],
  ])('keeps a link title with %s', (_, title) => {
    // Arrange
    const link = { type: 'link', attrs: { href: 'https://wayd.dev', title } }

    // Act
    const { reparsed } = roundTripParagraph({
      type: 'paragraph',
      content: [{ type: 'text', text: 'site', marks: [link] }],
    })

    // Assert
    expect(reparsed?.content?.[0].marks?.[0].attrs).toMatchObject({
      href: 'https://wayd.dev',
      title,
    })
  })

  it.each([
    ['brackets', 'a [b] c'],
    ['a backslash before a bracket', 'a\\]b'],
    ['a trailing backslash', 'folder\\'],
  ])('keeps image alt text with %s', (_, alt) => {
    // Arrange
    const image = {
      type: 'image',
      attrs: { src: 'https://wayd.dev/a.png', alt },
    }

    // Act
    const { reparsed } = roundTripParagraph({
      type: 'paragraph',
      content: [image],
    })

    // Assert
    expect(reparsed?.content?.[0].attrs).toMatchObject({
      src: 'https://wayd.dev/a.png',
      alt,
    })
  })
})
