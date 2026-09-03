import { downloadSvg, downloadSvgAsPng } from './export-svg'

const SVG =
  '<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"></svg>'

// Clicking a download link makes jsdom attempt a navigation it has not
// implemented. That failure surfaces as an unrelated Next AsyncLocalStorage
// invariant and poisons every later test in the file. Re-stubbed per test,
// since the suites below call restoreAllMocks.
beforeEach(() => {
  jest
    .spyOn(HTMLAnchorElement.prototype, 'click')
    .mockImplementation(() => undefined)
})

/** Capture the <a> the download path appends to the body. */
function lastDownloadLink(
  appendChild: jest.SpyInstance,
): HTMLAnchorElement | undefined {
  return appendChild.mock.calls
    .map(([node]) => node)
    .reverse()
    .find(
      (node): node is HTMLAnchorElement => node instanceof HTMLAnchorElement,
    )
}

describe('downloadSvg', () => {
  let appendChild: jest.SpyInstance

  beforeEach(() => {
    appendChild = jest.spyOn(document.body, 'appendChild')
    global.URL.createObjectURL = jest.fn(() => 'blob:svg')
    global.URL.revokeObjectURL = jest.fn()
  })

  afterEach(() => {
    document.body.innerHTML = ''
    jest.restoreAllMocks()
  })

  test('downloads the markup under the given filename', () => {
    // Arrange / Act
    downloadSvg(SVG, 'roadmap.svg')

    // Assert
    const link = lastDownloadLink(appendChild)
    expect(link?.download).toBe('roadmap.svg')
    expect(link?.href).toBe('blob:svg')
  })

  test('publishes the blob as SVG so browsers render it inline', () => {
    // Arrange / Act
    downloadSvg(SVG, 'roadmap.svg')

    // Assert — the wrong MIME type makes the file download as plain text.
    const blob = (global.URL.createObjectURL as jest.Mock).mock
      .calls[0][0] as Blob
    expect(blob.type).toContain('image/svg+xml')
  })

  test('writes the markup verbatim, with no byte-order mark', () => {
    // Arrange / Act
    downloadSvg(SVG, 'roadmap.svg')

    // Assert — a BOM renders as visible characters in consumers that ignore
    // the charset; the renderer escapes non-ASCII to numeric references
    // instead, so the payload is pure ASCII and needs no marker.
    const blob = (global.URL.createObjectURL as jest.Mock).mock
      .calls[0][0] as Blob
    expect(blob.size).toBe(SVG.length)
  })
})

describe('downloadSvgAsPng', () => {
  let appendChild: jest.SpyInstance

  /**
   * Make `new Image().src = …` resolve or fail on demand — jsdom never loads
   * images, so without this the export's onload never fires.
   *
   * `jest.spyOn(…, 'src', 'set')` (rather than a raw defineProperty) is what
   * lets `restoreAllMocks` put jsdom's own setter back; a hand-rolled
   * descriptor leaks into every later test in the file.
   */
  const stubImageLoad = (outcome: 'load' | 'error') => {
    jest
      .spyOn(global.Image.prototype, 'src', 'set')
      .mockImplementation(function (this: HTMLImageElement) {
        setTimeout(() => {
          if (outcome === 'load') this.onload?.(new Event('load'))
          else this.onerror?.(new Event('error'))
        }, 0)
      })
  }

  beforeEach(() => {
    appendChild = jest.spyOn(document.body, 'appendChild')
    global.URL.createObjectURL = jest.fn(() => 'blob:png')
    global.URL.revokeObjectURL = jest.fn()

    // jsdom neither loads images nor implements canvas 2D; stand both in.
    stubImageLoad('load')
    jest.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue({
      drawImage: jest.fn(),
    } as unknown as CanvasRenderingContext2D)
    jest
      .spyOn(HTMLCanvasElement.prototype, 'toBlob')
      .mockImplementation((cb: BlobCallback) => cb(new Blob(['png'])))
  })

  afterEach(() => {
    document.body.innerHTML = ''
    jest.restoreAllMocks()
  })

  test('rasterises the SVG and downloads it as a PNG', async () => {
    // Arrange / Act
    await downloadSvgAsPng(SVG, 'roadmap.png', { width: 100, height: 100 })

    // Assert
    const link = lastDownloadLink(appendChild)
    expect(link?.download).toBe('roadmap.png')
    expect(link?.href).toBe('blob:png')
  })

  /**
   * Record the backing size of the canvas the export rasterises into. Read via
   * `toBlob`'s `this` rather than by spying on `document.createElement`: that
   * spy re-enters jsdom's canvas construction and breaks the 2D-context stub
   * for this test AND every test after it.
   */
  const captureCanvasSize = () => {
    const size = { width: 0, height: 0 }
    jest
      .spyOn(HTMLCanvasElement.prototype, 'toBlob')
      .mockImplementation(function (this: HTMLCanvasElement, cb: BlobCallback) {
        size.width = this.width
        size.height = this.height
        cb(new Blob(['png']))
      })
    return size
  }

  test('scales the canvas past CSS size for a crisp image', async () => {
    // Arrange
    const size = captureCanvasSize()

    // Act
    await downloadSvgAsPng(SVG, 'roadmap.png', {
      width: 100,
      height: 100,
      scale: 2,
    })

    // Assert
    expect(size.width).toBe(200)
    expect(size.height).toBe(200)
  })

  test('reduces the scale rather than exceeding the canvas cap', async () => {
    // Arrange
    const size = captureCanvasSize()

    // Act — 2x here would blow the side cap, but 1x fits.
    await downloadSvgAsPng(SVG, 'roadmap.png', {
      width: 3840,
      height: 12_000,
      scale: 2,
    })

    // Assert — a softer image beats a blank one.
    expect(size.height).toBeLessThanOrEqual(16_384)
    expect(size.height).toBeGreaterThan(0)
  })

  test('refuses a timeline too large to rasterise at any scale', async () => {
    // Arrange — taller than a canvas can be even at 1x, so no scale rescues it.
    // The old path handed this to the browser and got a blank canvas back.

    // Act / Assert — must fail loudly, and point at the format that has no limit.
    await expect(
      downloadSvgAsPng(SVG, 'roadmap.png', { width: 3840, height: 20_000 }),
    ).rejects.toThrow(/SVG/i)
  })

  test('reports a helpful error when the PNG cannot be encoded', async () => {
    // Arrange
    jest
      .spyOn(HTMLCanvasElement.prototype, 'toBlob')
      .mockImplementation((cb: BlobCallback) => cb(null))

    // Act / Assert — the message must point at the SVG alternative.
    await expect(
      downloadSvgAsPng(SVG, 'roadmap.png', { width: 100, height: 100 }),
    ).rejects.toThrow(/SVG/i)
  })

  test('reports an error when the SVG itself fails to load', async () => {
    // Arrange
    stubImageLoad('error')

    // Act / Assert
    await expect(
      downloadSvgAsPng(SVG, 'roadmap.png', { width: 100, height: 100 }),
    ).rejects.toThrow(/could not render/i)
  })
})
