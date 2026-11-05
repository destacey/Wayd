import type { Editor } from '@tiptap/core'
import type { Slice } from '@tiptap/pm/model'
import {
  createTestEditor,
  placeCursorBefore,
  stubEditorLayout,
} from './editor-test-utils'
import { looksLikeMarkdown, withSafeUrls } from './markdown-paste'

/**
 * Pastes `text` as plain text with no HTML, as ProseMirror does: it asks `clipboardTextParser`
 * for a slice, falling back to its own handling (here: `null`) when none is given.
 */
const pastePlainText = (editor: Editor, text: string, plain = false) => {
  const { view } = editor
  const slice = view.someProp('clipboardTextParser', (parse) =>
    parse(text, view.state.selection.$from, plain, view),
  ) as Slice | null | undefined
  if (slice) view.dispatch(view.state.tr.replaceSelection(slice))
  return slice ?? null
}

describe('looksLikeMarkdown', () => {
  it.each([
    '# Heading',
    '- one\n- two',
    '1. first',
    '> quoted',
    '```\ncode\n```',
    '| a | b |',
    'some **bold** text',
    'a [link](https://wayd.dev)',
    'run `npm test`',
    '~~gone~~',
  ])('reads %p as markdown', (text) => {
    // Arrange / Act / Assert
    expect(looksLikeMarkdown(text)).toBe(true)
  })

  it.each([
    'Just a sentence.',
    'Two lines\nof prose',
    '2 * 3 * 4',
    'snake_case_name',
    'Q3 - planning',
    'https://wayd.dev',
  ])('reads %p as prose', (text) => {
    // Arrange / Act / Assert
    expect(looksLikeMarkdown(text)).toBe(false)
  })
})

describe('withSafeUrls', () => {
  it('drops an unsafe link but keeps its text, and drops an unsafe image', () => {
    // Arrange
    const content = {
      type: 'paragraph',
      content: [
        {
          type: 'text',
          text: 'x',
          marks: [{ type: 'link', attrs: { href: 'javascript:alert(1)' } }],
        },
        { type: 'image', attrs: { src: 'data:image/png;base64,AA' } },
        {
          type: 'text',
          text: 'y',
          marks: [{ type: 'link', attrs: { href: 'https://wayd.dev' } }],
        },
      ],
    }

    // Act
    const safe = withSafeUrls(content)

    // Assert
    expect(safe.content).toEqual([
      { type: 'text', text: 'x', marks: [] },
      {
        type: 'text',
        text: 'y',
        marks: [{ type: 'link', attrs: { href: 'https://wayd.dev' } }],
      },
    ])
  })
})

describe('MarkdownPaste', () => {
  let editor: Editor | undefined

  beforeAll(stubEditorLayout)

  afterEach(() => {
    editor?.destroy()
    editor = undefined
  })

  it('formats pasted markdown blocks', () => {
    // Arrange
    editor = createTestEditor('')
    const markdown = '## Plan\n\n- one\n- two\n\nSome **bold** text'

    // Act
    pastePlainText(editor, markdown)

    // Assert
    expect(editor.getMarkdown().trim()).toBe(markdown)
  })

  it('joins pasted inline markdown to the line it is pasted into', () => {
    // Arrange
    editor = createTestEditor('Before after')
    placeCursorBefore(editor, 'after')

    // Act
    pastePlainText(editor, 'some **bold** ')

    // Assert
    expect(editor.getMarkdown().trim()).toBe('Before some **bold** after')
  })

  it('keeps the text but not the address of an unsafe pasted link', () => {
    // Arrange
    editor = createTestEditor('')

    // Act
    pastePlainText(editor, '[click](javascript:alert) and **this**')

    // Assert
    expect(editor.getMarkdown().trim()).toBe('click and **this**')
  })

  it.each([
    ['prose', 'Just a sentence.', false],
    ['a paste without formatting', '**bold**', true],
  ])('leaves %s to the default handling', (_, text, plain) => {
    // Arrange
    editor = createTestEditor('')

    // Act
    const slice = pastePlainText(editor, text, plain)

    // Assert
    expect(slice).toBeNull()
  })

  // Through ProseMirror's own paste path, so the editor's paste rules run as they do on a real paste.
  it.each([
    [
      'text pasted without formatting',
      (e: Editor) =>
        e.view.pasteText('2 * 3 * 4 and **x**', new Event('paste') as never),
    ],
    [
      'HTML',
      (e: Editor) =>
        e.view.pasteHTML(
          '<p>2 * 3 * 4 and **x**</p>',
          new Event('paste') as never,
        ),
    ],
  ])('keeps asterisks in %s as text', (_, paste) => {
    // Arrange
    editor = createTestEditor('')

    // Act
    paste(editor)

    // Assert
    expect(editor.getText()).toBe('2 * 3 * 4 and **x**')
  })
})
