import { imageFileFromDataUrl } from './attachments'

vi.mock(import('@/src/services/api/attachments'), () => ({
  attachmentsGetContent: vi.fn(),
  attachmentsUpload: vi.fn(),
}))

const bytesOf = async (file: File) =>
  Array.from(new Uint8Array(await file.arrayBuffer()))

describe('imageFileFromDataUrl', () => {
  it('decodes a base64 image into a file of its type', async () => {
    // Arrange — the URL is the whole input.

    // Act
    const file = imageFileFromDataUrl(
      'data:image/png;base64,AAEC/w==',
      'pasted-image-1',
    )

    // Assert
    expect(file?.name).toBe('pasted-image-1.png')
    expect(file?.type).toBe('image/png')
    expect(await bytesOf(file!)).toEqual([0, 1, 2, 255])
  })

  it('decodes a percent-encoded image', async () => {
    // Arrange — the URL is the whole input.

    // Act
    const file = imageFileFromDataUrl(
      'data:image/svg+xml,%3Csvg%2F%3E',
      'pasted-image-1',
    )

    // Assert — refusing the type is the uploader's decision, not the decoder's.
    expect(file?.name).toBe('pasted-image-1.svg')
    expect(file?.type).toBe('image/svg+xml')
    expect(await file?.text()).toBe('<svg/>')
  })

  it('ignores whitespace that wraps a long base64 payload', async () => {
    // Arrange — the URL is the whole input.

    // Act
    const file = imageFileFromDataUrl('data:image/jpeg;base64,AAEC\n/w==', 'x')

    // Assert
    expect(await bytesOf(file!)).toEqual([0, 1, 2, 255])
  })

  it('keeps each percent-encoded byte, including bytes that are not text', async () => {
    // Arrange — a PNG's signature starts with 0x89, which is not valid UTF-8 on its own.
    const src = 'data:image/png,%89PNG%FF'

    // Act
    const file = imageFileFromDataUrl(src, 'x')

    // Assert
    expect(await bytesOf(file!)).toEqual([0x89, 0x50, 0x4e, 0x47, 0xff])
  })

  it.each([
    ['https://wayd.dev/logo.png'],
    ['file:///C:/Users/me/clip_image001.png'],
    ['data:text/html;base64,PHA+PC9wPg=='],
    ['data:image/png;base64,not base64!'],
    ['data:image/png;base64'],
  ])('answers null for %s, which holds no image', (src) => {
    // Arrange — the URL is the whole input.

    // Act
    const file = imageFileFromDataUrl(src, 'x')

    // Assert
    expect(file).toBeNull()
  })
})
