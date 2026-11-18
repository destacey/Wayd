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
import { attachmentsUpload } from '@/src/services/api/attachments'
import { uploadImage } from '../attachments'

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

  describe('images in pasted HTML', () => {
    /** A `data:` URL of a PNG-typed image large enough not to be taken for a spacer. */
    const DATA_PNG = `data:image/png;base64,${btoa('x'.repeat(300))}`
    const OTHER_PATH = '/api/attachments/0190a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5c'
    const LOST_ONE =
      'An image in the pasted text could not be added. Copy and paste each image on its own.'

    const pasteHtml = (html: string) =>
      editor.view.pasteHTML(html, new Event('paste') as never)

    const hasMarker = () => editor.state.doc.textContent.includes('\uFFFC')

    afterEach(() => vi.unstubAllGlobals())

    it('uploads an image embedded in pasted text and places it where it sat, with its alt text', async () => {
      // Arrange
      const upload = deferred<{ path: string }>()
      const uploader = vi.fn((_file: File) => upload.promise)
      setMarkdownImageUpload(editor, uploader)

      // Act
      pasteHtml(`<p>one <img src="${DATA_PNG}" alt="chart"> two</p>`)

      // Assert — the placeholder holds the place, and nothing of the marker is left in the text.
      expect(uploader).toHaveBeenCalledTimes(1)
      const file = uploader.mock.calls[0][0]
      expect(file.type).toBe('image/png')
      expect(file.name).toBe('pasted-image-1.png')
      expect(placeholder(editor)).not.toBeNull()
      expect(hasMarker()).toBe(false)

      upload.resolve({ path: PATH })
      await vi.waitFor(() =>
        expect(editor.getMarkdown()).toBe(
          `before one ![chart](${PATH}) twoafter`,
        ),
      )
      expect(placeholder(editor)).toBeNull()
    })

    it('places each of several embedded images where it sat', async () => {
      // Arrange
      const paths = [PATH, OTHER_PATH]
      let next = 0
      setMarkdownImageUpload(editor, async () => ({ path: paths[next++] }))

      // Act
      pasteHtml(
        `<p>a <img src="${DATA_PNG}" alt="first"> b <img src="${DATA_PNG}" alt="second"> c</p>`,
      )

      // Assert
      await vi.waitFor(() =>
        expect(editor.getMarkdown()).toBe(
          `before a ![first](${PATH}) b ![second](${OTHER_PATH}) cafter`,
        ),
      )
    })

    it('keeps two images side by side in order when the second upload answers first', async () => {
      // Arrange
      const uploads = [
        deferred<{ path: string }>(),
        deferred<{ path: string }>(),
      ]
      let next = 0
      setMarkdownImageUpload(editor, () => uploads[next++].promise)
      pasteHtml(
        `<p><img src="${DATA_PNG}" alt="1"><img src="${DATA_PNG}" alt="2"></p>`,
      )

      // Act
      uploads[1].resolve({ path: OTHER_PATH })
      await uploads[1].promise
      uploads[0].resolve({ path: PATH })

      // Assert
      await vi.waitFor(() =>
        expect(editor.getMarkdown()).toBe(
          `before ![1](${PATH})![2](${OTHER_PATH})after`,
        ),
      )
    })

    it('says how many images it could not add, and pastes the text around them', () => {
      // Arrange — Word refers to its images by a path on the copier's own disk.
      const uploader = vi.fn()
      const onError = vi.fn()
      setMarkdownImageUpload(editor, uploader, onError)

      // Act
      pasteHtml(
        '<p>one <img src="file:///C:/Users/me/clip_image001.png"> two <img src="blob:https://x/1"> three</p>',
      )

      // Assert
      expect(uploader).not.toHaveBeenCalled()
      expect(onError.mock.calls).toEqual([
        [
          '2 images in the pasted text could not be added. Copy and paste each image on its own.',
        ],
      ])
      expect(editor.getMarkdown()).toBe('before one two threeafter')
    })

    it('names a single image it could not add', () => {
      // Arrange
      const onError = vi.fn()
      setMarkdownImageUpload(editor, vi.fn(), onError)

      // Act
      pasteHtml('<p>one <img src="file:///C:/a.png"> two</p>')

      // Assert
      expect(onError).toHaveBeenLastCalledWith(LOST_ONE)
    })

    it('uploads the embedded image and reports the unreachable one of the same paste, in one message', () => {
      // Arrange
      const uploader = vi.fn(() => new Promise<{ path: string }>(() => {}))
      const onError = vi.fn()
      setMarkdownImageUpload(editor, uploader, onError)

      // Act
      pasteHtml(
        `<p>one <img src="${DATA_PNG}"> two <img src="file:///C:/a.png"> three</p>`,
      )

      // Assert
      expect(uploader).toHaveBeenCalledTimes(1)
      expect(placeholder(editor)).not.toBeNull()
      expect(onError.mock.calls).toEqual([[LOST_ONE]])
    })

    it('shows every reason images were left out, each once', () => {
      // Arrange
      const svg = `data:image/svg+xml,${encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg">${' '.repeat(200)}</svg>`)}`
      const onError = vi.fn()
      setMarkdownImageUpload(editor, vi.fn(), onError)

      // Act
      pasteHtml(
        `<p><img src="${svg}"> <img src="${svg}"> <img src="file:///C:/a.png"></p>`,
      )

      // Assert
      expect(onError.mock.calls).toEqual([
        [`Only PNG, JPEG, GIF and WebP images can be added. ${LOST_ONE}`],
      ])
      expect(placeholder(editor)).toBeNull()
      expect(hasMarker()).toBe(false)
    })

    it('refuses an embedded image far over the size limit without decoding it', () => {
      // Arrange
      const uploader = vi.fn()
      const onError = vi.fn()
      setMarkdownImageUpload(editor, uploader, onError)
      const huge = `data:image/png;base64,${'A'.repeat(8 * 1024 * 1024)}`

      // Act
      pasteHtml(`<p>one <img src="${huge}"> two</p>`)

      // Assert
      expect(uploader).not.toHaveBeenCalled()
      expect(onError).toHaveBeenLastCalledWith('Images can be at most 5 MB.')
      expect(editor.getMarkdown()).toBe('before one  twoafter')
    })

    it('removes the placeholder and says why when an embedded image fails to upload', async () => {
      // Arrange
      const upload = deferred<{ path: string }>()
      const onError = vi.fn()
      setMarkdownImageUpload(editor, () => upload.promise, onError)
      pasteHtml(`<p>one <img src="${DATA_PNG}"> two</p>`)

      // Act
      upload.reject({
        status: 400,
        detail: 'The file is not a PNG, JPEG, GIF or WebP image.',
      })

      // Assert
      await vi.waitFor(() =>
        expect(onError).toHaveBeenLastCalledWith(
          'The file is not a PNG, JPEG, GIF or WebP image.',
        ),
      )
      expect(placeholder(editor)).toBeNull()
      expect(editor.getMarkdown()).toBe('before one  twoafter')
    })

    it('places an embedded image inside a table cell', async () => {
      // Arrange
      setMarkdownImageUpload(editor, async () => ({ path: PATH }))

      // Act
      pasteHtml(
        `<table><tr><th>a</th></tr><tr><td>x <img src="${DATA_PNG}" alt="cell"></td></tr></table>`,
      )

      // Assert
      await vi.waitFor(() =>
        expect(editor.getMarkdown()).toContain(`x ![cell](${PATH})`),
      )
      expect(hasMarker()).toBe(false)
    })

    it('writes alt text with brackets so the markdown still reads as one image', async () => {
      // Arrange
      setMarkdownImageUpload(editor, async () => ({ path: PATH }))

      // Act
      pasteHtml(`<p><img src="${DATA_PNG}" alt="a ] b"></p>`)

      // Assert
      await vi.waitFor(() =>
        expect(editor.getMarkdown()).toBe(`before ![a \\] b](${PATH})after`),
      )
    })

    it('undoes the paste and its placeholders in one step', () => {
      // Arrange
      setMarkdownImageUpload(editor, () => new Promise(() => {}))
      pasteHtml(`<p>one <img src="${DATA_PNG}"> two</p>`)

      // Act
      editor.commands.undo()

      // Assert
      expect(editor.getMarkdown()).toBe('before after')
      expect(placeholder(editor)).toBeNull()
    })

    it('uploads an embedded image dropped with HTML where it was dropped', async () => {
      // Arrange
      setMarkdownImageUpload(editor, async () => ({ path: PATH }))
      const html = `<p>one <img src="${DATA_PNG}" alt="d"> two</p>`
      editor.view.posAtCoords = () => ({ pos: 8, inside: -1 })
      const event = new Event('drop', { cancelable: true, bubbles: true })
      Object.defineProperty(event, 'dataTransfer', {
        value: {
          files: [],
          types: ['text/html'],
          getData: (type: string) => (type === 'text/html' ? html : ''),
        },
      })

      // Act
      editor.view.dom.dispatchEvent(event)

      // Assert
      await vi.waitFor(() =>
        expect(editor.getMarkdown()).toBe(`before one ![d](${PATH}) twoafter`),
      )
    })

    it('leaves a web page spacer out without a word', () => {
      // Arrange
      const uploader = vi.fn()
      const onError = vi.fn()
      setMarkdownImageUpload(editor, uploader, onError)
      const spacer =
        'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7'

      // Act
      pasteHtml(`<p>one <img src="${spacer}"> two</p>`)

      // Assert
      expect(uploader).not.toHaveBeenCalled()
      expect(onError).not.toHaveBeenCalledWith(expect.any(String))
      expect(editor.getMarkdown()).toBe('before one twoafter')
    })

    it('keeps the web address a lazy-loading page held back behind its placeholder', () => {
      // Arrange
      const uploader = vi.fn()
      setMarkdownImageUpload(editor, uploader)

      // Act
      pasteHtml(
        `<p><img src="${DATA_PNG}" data-src="https://wayd.dev/real.png" alt="lazy"></p>`,
      )

      // Assert
      expect(uploader).not.toHaveBeenCalled()
      expect(editor.getMarkdown()).toBe(
        'before ![lazy](https://wayd.dev/real.png)after',
      )
    })

    it('keeps an image copied from what Wayd shows, by its attachment path', async () => {
      // Arrange — Wayd shows an uploaded image from an object URL, which is what a copy carries.
      vi.stubGlobal(
        'URL',
        Object.assign(URL, {
          createObjectURL: () => 'blob:http://localhost/shown',
        }),
      )
      vi.mocked(attachmentsUpload).mockResolvedValue({
        id: PATH.split('/').pop()!,
      } as never)
      await uploadImage(png())
      const onError = vi.fn()
      setMarkdownImageUpload(editor, vi.fn(), onError)

      // Act
      pasteHtml('<p><img src="blob:http://localhost/shown" alt="shown"></p>')

      // Assert
      expect(editor.getMarkdown()).toBe(`before ![shown](${PATH})after`)
      expect(onError).not.toHaveBeenCalledWith(expect.any(String))
    })

    it('takes the image file of a paste whose HTML names only an image no one can fetch', () => {
      // Arrange — Word copying a single picture puts both on the clipboard.
      const uploader = vi.fn(() => new Promise<{ path: string }>(() => {}))
      setMarkdownImageUpload(editor, uploader)
      const file = png()

      // Act
      const result = paste(
        editor,
        [file],
        '<p><img src="file:///C:/a.png"></p>',
      )

      // Assert
      expect(result.handled).toBe(true)
      expect(uploader).toHaveBeenCalledWith(file)
    })

    it('leaves web and uploaded images to the editor, with nothing to report', () => {
      // Arrange
      const uploader = vi.fn()
      const onError = vi.fn()
      setMarkdownImageUpload(editor, uploader, onError)

      // Act
      pasteHtml(
        `<p><img src="https://wayd.dev/logo.png" alt="web"> <img src="${PATH}" alt="ours"></p>`,
      )

      // Assert
      expect(uploader).not.toHaveBeenCalled()
      expect(onError).not.toHaveBeenCalled()
      expect(editor.getMarkdown()).toContain(
        '![web](https://wayd.dev/logo.png)',
      )
      expect(editor.getMarkdown()).toContain(`![ours](${PATH})`)
    })

    it('leaves embedded images out without a word in a field that takes no uploads', () => {
      // Arrange
      setMarkdownImageUpload(editor, undefined)

      // Act
      pasteHtml(`<p>one <img src="${DATA_PNG}"> two</p>`)

      // Assert
      expect(editor.getMarkdown()).toBe('before one twoafter')
      expect(hasMarker()).toBe(false)
    })

    it('does not carry the images of a paste the length limit refused into the next paste', async () => {
      // Arrange
      const uploader = vi.fn()
      const onError = vi.fn()
      setMarkdownLengthLimit(editor, 14, vi.fn())
      setMarkdownImageUpload(editor, uploader, onError)
      pasteHtml('<p>far too long a paste <img src="file:///C:/a.png"></p>')
      expect(editor.getMarkdown()).toBe('before after')
      await Promise.resolve()

      // Act
      editor.view.pasteText('x', new Event('paste') as never)

      // Assert
      expect(editor.getMarkdown()).toBe('before xafter')
      expect(onError).not.toHaveBeenCalled()
      expect(uploader).not.toHaveBeenCalled()
    })

    it('treats marker-like text already in the field as text', () => {
      // Arrange
      editor.commands.setContent('kept \uFFFC0\uFFFC here')
      editor.commands.setTextSelection(editor.state.doc.content.size - 1)
      const uploader = vi.fn(() => new Promise<{ path: string }>(() => {}))
      setMarkdownImageUpload(editor, uploader)

      // Act
      pasteHtml(`<p><img src="${DATA_PNG}"></p>`)

      // Assert
      expect(editor.state.doc.textContent).toContain('kept \uFFFC0\uFFFC here')
      expect(uploader).toHaveBeenCalledTimes(1)
    })
  })
})
