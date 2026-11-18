import { imageFileFromDataUrl, isSpacerImage } from './attachments'

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

/** The first bytes of a PNG of `width`×`height`: its signature and image header. */
const pngHeader = (width: number, height: number) => {
  const bytes = new Uint8Array(33)
  bytes.set([
    0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a, 0, 0, 0, 13, 0x49, 0x48,
    0x44, 0x52,
  ])
  const view = new DataView(bytes.buffer)
  view.setUint32(16, width)
  view.setUint32(20, height)
  return bytes
}

/** The first bytes of a GIF of `width`×`height`. */
const gifHeader = (width: number, height: number) => {
  const bytes = new Uint8Array(13)
  bytes.set([0x47, 0x49, 0x46, 0x38, 0x39, 0x61])
  const view = new DataView(bytes.buffer)
  view.setUint16(6, width, true)
  view.setUint16(8, height, true)
  return bytes
}

describe('isSpacerImage', () => {
  it.each([
    ['a 1×1 GIF', gifHeader(1, 1)],
    ['a 1×1 PNG', pngHeader(1, 1)],
    ['a 2×2 PNG', pngHeader(2, 2)],
  ])('takes %s for a spacer', (_name, bytes) => {
    // Arrange — the header is the whole input.

    // Act
    const spacer = isSpacerImage(bytes)

    // Assert
    expect(spacer).toBe(true)
  })

  it.each([
    ['a 20×20 PNG, however few bytes it takes', pngHeader(20, 20)],
    ['a 1×40 GIF divider', gifHeader(1, 40)],
    ['a JPEG', Uint8Array.from([0xff, 0xd8, 0xff, 0xe0])],
    ['bytes that are no image', new Uint8Array(4)],
  ])('keeps %s', (_name, bytes) => {
    // Arrange — the header is the whole input.

    // Act
    const spacer = isSpacerImage(bytes)

    // Assert
    expect(spacer).toBe(false)
  })
})
