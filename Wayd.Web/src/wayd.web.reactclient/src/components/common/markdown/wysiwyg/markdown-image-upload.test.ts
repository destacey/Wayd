// @vitest-environment jsdom
import type { Editor } from '@tiptap/core'
import {
  createTestEditor,
  placeCursorBefore,
  selectText,
  stubEditorLayout,
  typeText,
} from './editor-test-utils'
import { setMarkdownLengthLimit } from './markdown-length-limit'
import { setMarkdownImageUpload } from './markdown-image-upload'

vi.mock(import('@/src/services/api/attachments'), () => ({
  attachmentsGetContent: vi.fn(() => new Promise<Blob>(() => {})),
  attachmentsUpload: vi.fn(),
}))

const PATH = '/api/attachments/0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b'

const png = (name = 'screenshot.png', size = 10) =>
  new File([new Uint8Array(size)], name, { type: 'image/png' })

/** Pastes a clipboard holding `files`, and HTML too when given, as the browser does. */
const paste = (editor: Editor, files: File[], html?: string) => {
  const { view } = editor
  const clipboardData = {
    files,
    types: [...(html ? ['text/html'] : []), ...(files.length ? ['Files'] : [])],
    getData: (type: string) => (type === 'text/html' ? (html ?? '') : ''),
  } as unknown as DataTransfer
  const event = new Event('paste', { cancelable: true }) as ClipboardEvent
  Object.defineProperty(event, 'clipboardData', { value: clipboardData })
  const handled = view.someProp('handlePaste', (handler) =>
    handler(view, event, view.state.doc.slice(0, 0)),
  )
  return { handled: !!handled, prevented: event.defaultPrevented }
}

/** A promise with its settle functions, so a test decides when an upload finishes. */
const deferred = <T>() => {
  let resolve!: (value: T) => void
  let reject!: (reason: unknown) => void
  const promise = new Promise<T>((res, rej) => {
    resolve = res
    reject = rej
  })
  return { promise, resolve, reject }
}

const placeholder = (editor: Editor) =>
  editor.view.dom.querySelector('.markdown-image-uploading')

describe('MarkdownImageUpload', () => {
  let editor: Editor

  beforeAll(stubEditorLayout)

  beforeEach(() => {
    editor = createTestEditor('before after')
    placeCursorBefore(editor, 'after')
  })

  afterEach(() => editor.destroy())

  it('uploads a pasted image and inserts it where it was pasted, with a placeholder meanwhile', async () => {
    // Arrange
    const upload = deferred<{ path: string }>()
    const uploader = vi.fn(() => upload.promise)
    setMarkdownImageUpload(editor, uploader)
    const file = png()

    // Act
    const result = paste(editor, [file])

    // Assert — the placeholder holds the place while the upload runs.
    expect(result).toEqual({ handled: true, prevented: true })
    expect(uploader).toHaveBeenCalledWith(file)
    expect(placeholder(editor)?.textContent).toBe('Uploading image…')
    expect(editor.getMarkdown()).toBe('before after')

    upload.resolve({ path: PATH })
    await upload.promise
    await Promise.resolve()

    expect(placeholder(editor)).toBeNull()
    expect(editor.getMarkdown()).toBe(`before ![](${PATH})after`)
  })

  it('removes the placeholder and says why when the upload fails', async () => {
    // Arrange
    const upload = deferred<{ path: string }>()
    const onError = vi.fn()
    setMarkdownImageUpload(editor, () => upload.promise, onError)
    paste(editor, [png()])

    // Act
    upload.reject({
      status: 400,
      detail: 'The file is not a PNG, JPEG, GIF or WebP image.',
    })
    await upload.promise.catch(() => {})
    await Promise.resolve()

    // Assert
    expect(placeholder(editor)).toBeNull()
    expect(onError).toHaveBeenCalledWith(
      'The file is not a PNG, JPEG, GIF or WebP image.',
    )
    expect(editor.getMarkdown()).toBe('before after')
  })

  it('refuses an image over 5 MB without uploading it', () => {
    // Arrange
    const uploader = vi.fn()
    const onError = vi.fn()
    setMarkdownImageUpload(editor, uploader, onError)

    // Act
    paste(editor, [png('huge.png', 5 * 1024 * 1024 + 1)])

    // Assert
    expect(uploader).not.toHaveBeenCalled()
    expect(onError).toHaveBeenCalledWith('Images can be at most 5 MB.')
    expect(placeholder(editor)).toBeNull()
  })

  it('leaves a paste that carries HTML too to the editor, as a spreadsheet copy does', () => {
    // Arrange
    const uploader = vi.fn()
    setMarkdownImageUpload(editor, uploader)

    // Act
    const result = paste(editor, [png()], '<table><tr><td>1</td></tr></table>')

    // Assert
    expect(result.handled).toBe(false)
    expect(uploader).not.toHaveBeenCalled()
  })

  it('leaves images to the editor while uploads are off', () => {
    // Arrange
    setMarkdownImageUpload(editor, undefined)

    // Act
    const result = paste(editor, [png()])

    // Assert
    expect(result).toEqual({ handled: false, prevented: false })
  })

  it('removes the placeholder when the length limit refuses the image', async () => {
    // Arrange — "before after" is 12 characters; the image's markdown would take it far past 20.
    const upload = deferred<{ path: string }>()
    const onRefuse = vi.fn()
    setMarkdownLengthLimit(editor, 20, onRefuse)
    setMarkdownImageUpload(editor, () => upload.promise)
    paste(editor, [png()])

    // Act
    upload.resolve({ path: PATH })
    await upload.promise
    await Promise.resolve()

    // Assert
    expect(placeholder(editor)).toBeNull()
    expect(onRefuse).toHaveBeenCalled()
    expect(editor.getMarkdown()).toBe('before after')
  })

  it('inserts the image where it was pasted after text is typed before it during the upload', async () => {
    // Arrange
    const upload = deferred<{ path: string }>()
    setMarkdownImageUpload(editor, () => upload.promise)
    paste(editor, [png()])

    // Act
    editor.commands.setTextSelection(1)
    typeText(editor, 'new ')
    upload.resolve({ path: PATH })
    await upload.promise
    await Promise.resolve()

    // Assert
    expect(editor.getMarkdown()).toBe(`new before ![](${PATH})after`)
  })

  it('replaces the selection, as any paste does', () => {
    // Arrange
    setMarkdownImageUpload(editor, () => new Promise(() => {}))
    selectText(editor, 'after')

    // Act
    paste(editor, [png()])

    // Assert
    expect(editor.getMarkdown()).toBe('before ')
    expect(placeholder(editor)).not.toBeNull()
  })

  it("keeps one file's refusal on screen when another file of the same paste uploads", async () => {
    // Arrange
    const onError = vi.fn()
    setMarkdownImageUpload(editor, async () => ({ path: PATH }), onError)

    // Act
    paste(editor, [png('huge.png', 5 * 1024 * 1024 + 1), png()])
    await Promise.resolve()
    await Promise.resolve()

    // Assert — cleared once as the paste starts, then the refusal; the later success clears nothing.
    expect(onError.mock.calls).toEqual([
      [null],
      ['Images can be at most 5 MB.'],
    ])
    expect(editor.getMarkdown()).toBe(`before ![](${PATH})after`)
  })

  it('leaves an uploaded image out of a paste into a field that takes no uploads', () => {
    // Arrange
    setMarkdownImageUpload(editor, undefined)

    // Act
    editor.view.pasteHTML(
      `<p>kept <img src="${PATH}" alt="a"> <img src="https://wayd.dev/logo.png" alt="b"></p>`,
      new Event('paste') as never,
    )

    // Assert
    const markdown = editor.getMarkdown()
    expect(markdown).not.toContain('/api/attachments/')
    expect(markdown).toContain('https://wayd.dev/logo.png')
  })

  it('keeps an uploaded image in a paste into a field that takes uploads', () => {
    // Arrange
    setMarkdownImageUpload(editor, () => new Promise(() => {}))

    // Act
    editor.view.pasteHTML(
      `<p><img src="${PATH}" alt="a"></p>`,
      new Event('paste') as never,
    )

    // Assert
    expect(editor.getMarkdown()).toContain(`![a](${PATH})`)
  })

  it('makes no image of a typed attachment path in a field that takes no uploads', () => {
    // Arrange
    setMarkdownImageUpload(editor, undefined)
    editor.commands.setTextSelection(editor.state.doc.content.size - 1)

    // Act
    typeText(editor, ` ![a](${PATH})`)

    // Assert
    expect(editor.state.doc.textContent).toContain(`![a](${PATH})`)
    let hasImage = false
    editor.state.doc.descendants((node) => {
      if (node.type.name === 'image') hasImage = true
    })
    expect(hasImage).toBe(false)
  })

  it('renders an uploaded image as HTML without a src, so nothing asks this site for it', () => {
    // Arrange
    const withImage = createTestEditor(`text ![a](${PATH})`)

    // Act
    const html = withImage.getHTML()
    withImage.destroy()

    // Assert
    expect(html).toContain(`data-attachment-src="${PATH}"`)
    expect(html).not.toMatch(/\ssrc=/)
  })

  it('keeps an uploaded image copied from another upload field', () => {
    // Arrange — what a copy from the editor puts on the clipboard.
    setMarkdownImageUpload(editor, () => new Promise(() => {}))

    // Act
    editor.view.pasteHTML(
      `<p><img data-attachment-src="${PATH}" alt="a"></p>`,
      new Event('paste') as never,
    )

    // Assert
    expect(editor.getMarkdown()).toContain(`![a](${PATH})`)
  })
})
