// timeline/render/svg/export-svg.ts
// Turns a rendered SVG string into a downloaded file — either the SVG itself,
// or a PNG rasterised FROM that SVG.
//
// Rasterising the vector is strictly better than screenshotting the DOM: there
// is no document clone, so no reflow, no lost Splitter widths, and no
// media-query re-evaluation at a viewport the user isn't using. The PNG is a
// faithful scaling of the same geometry the screen shows.

import { exceedsBudgetAtAnyScale, fitScaleToBudget } from '../capture-limits'

/** Shown whenever a PNG can't be produced. SVG has no size limit, so it is
 *  always the honest fallback rather than a dead end. */
const TOO_LARGE_FOR_PNG =
  'The timeline is too large to export as a PNG. Save it as SVG instead — it stays sharp at any size — or collapse some groups and try again.'

export type TimelineExportFormat = 'png' | 'svg'

/** Trigger a browser download for `blob`, then release the object URL. */
function downloadBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  // Revoking in the same task can cancel the download in some browsers.
  setTimeout(() => URL.revokeObjectURL(url), 0)
}

/**
 * Save the SVG document as-is. Resolution-independent and tiny.
 *
 * No BOM: a consumer that ignores the charset renders the marker as visible
 * characters rather than honouring it. The renderer escapes every non-ASCII
 * character to a numeric reference instead, so the bytes are pure ASCII and
 * decode identically however the file is read.
 */
export function downloadSvg(svg: string, fileName: string) {
  downloadBlob(
    new Blob([svg], { type: 'image/svg+xml;charset=utf-8' }),
    fileName,
  )
}

/**
 * Rasterise an SVG string to a PNG and download it.
 *
 * The SVG is loaded via a data URL rather than a blob URL: a blob URL taints
 * the canvas in some browsers, which makes the subsequent `toBlob` throw a
 * security error. A data URL of the same-origin markup does not.
 */
export async function downloadSvgAsPng(
  svg: string,
  fileName: string,
  spec: { width: number; height: number; scale?: number },
): Promise<void> {
  // Some timelines are past a canvas's reach even at 1x. Refuse up front rather
  // than hand the browser an over-cap canvas, which comes back blank instead of
  // throwing and would download an empty file.
  if (exceedsBudgetAtAnyScale(spec.width, spec.height)) {
    throw new Error(TOO_LARGE_FOR_PNG)
  }

  const requested = spec.scale ?? Math.max(window.devicePixelRatio || 1, 2)
  // Rasterising still lands in a canvas, so the same size ceiling applies here
  // as for the old screenshot path — degrade sharpness rather than emit a blank
  // image. The SVG export has no such limit, which is the honest way out for a
  // timeline too large to rasterise well.
  const scale = fitScaleToBudget(spec.width, spec.height, requested)

  const encoded = `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`
  const image = new Image()
  // Belt and braces: the markup is inlined, but this keeps the canvas clean
  // even if a future SVG references an external asset.
  image.crossOrigin = 'anonymous'

  await new Promise<void>((resolve, reject) => {
    image.onload = () => resolve()
    image.onerror = () =>
      reject(new Error('Could not render the timeline image.'))
    image.src = encoded
  })

  const canvas = document.createElement('canvas')
  canvas.width = Math.ceil(spec.width * scale)
  canvas.height = Math.ceil(spec.height * scale)
  const ctx = canvas.getContext('2d')
  if (!ctx) throw new Error('Could not render the timeline image.')
  ctx.drawImage(image, 0, 0, canvas.width, canvas.height)

  const blob = await new Promise<Blob | null>((resolve) => {
    canvas.toBlob(resolve, 'image/png')
  })
  if (!blob) {
    throw new Error(TOO_LARGE_FOR_PNG)
  }
  downloadBlob(blob, fileName)
}
