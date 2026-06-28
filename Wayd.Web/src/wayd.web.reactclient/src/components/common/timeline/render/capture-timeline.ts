// timeline/render/capture-timeline.ts
// Save-as-image. We capture the CURRENT horizontal viewport (no off-screen time)
// but the FULL vertical extent (all rows, as if there were no vertical scroll).
// Because we don't expand horizontally, the Splitter/flex layout stays valid, so
// we capture the chart container as ONE element and only un-clip the vertical
// scroll inside html2canvas's cloned document (onclone) — the live view never
// reflows. No split-and-stitch needed.

export interface CaptureOptions {
  fileName: string
  backgroundColor?: string
  /** Full content height to capture (axis + all rows + borders), px. */
  captureHeight: number
  /**
   * Live pixel width of the group-label column pane. antd's Splitter sizes its
   * panels via JS at runtime; those widths don't survive html2canvas's clone,
   * so the cloned group column can collapse to its min width and wrap labels.
   * We re-pin it explicitly in the clone to match the on-screen layout.
   */
  groupPaneWidth?: number
  /** Device-scale multiplier for crisp output. */
  scale?: number
  /**
   * Optional element rendered by the consumer below the timeline (e.g. a color
   * legend). It lives outside `root` in the live DOM, so we capture it separately
   * and stitch it underneath the chart in the exported image.
   */
  footer?: HTMLElement
}

export async function captureTimeline(
  root: HTMLElement,
  options: CaptureOptions,
) {
  const { default: html2canvas } = await import('html2canvas')
  const scale = options.scale ?? Math.max(window.devicePixelRatio || 1, 2)
  const { groupPaneWidth } = options

  const width = Math.ceil(root.getBoundingClientRect().width)
  const height = Math.ceil(options.captureHeight)

  const chartCanvas = await html2canvas(root, {
    backgroundColor: options.backgroundColor ?? null,
    scale,
    width,
    height,
    // windowHeight must be >= height so html2canvas doesn't clip the render to
    // the live element's viewport-constrained bounds.
    windowWidth: width,
    windowHeight: Math.max(height, window.innerHeight),
    // Neutralise any page scroll offset so the capture starts at the element.
    scrollY: -window.scrollY,
    scrollX: -window.scrollX,
    onclone: (doc: Document, clonedRoot: HTMLElement) => {
      // Clear body/html margin/padding to avoid layout offsets
      doc.body.style.margin = '0'
      doc.body.style.padding = '0'
      doc.documentElement.style.margin = '0'
      doc.documentElement.style.padding = '0'

      // Give the cloned document body enough room so absolutely/flex positioned
      // descendants can expand to the full content height without clipping.
      doc.body.style.height = `${height}px`
      doc.body.style.minHeight = `${height}px`
      doc.documentElement.style.height = `${height}px`
      doc.body.style.width = `${width}px`
      doc.body.style.minWidth = `${width}px`
      doc.documentElement.style.width = `${width}px`

      // Set the root to exactly the content height and width — no blank space below the
      // last row, and no clipping if content is taller than the live element.
      clonedRoot.style.height = `${height}px`
      clonedRoot.style.minHeight = 'unset'
      clonedRoot.style.maxHeight = 'unset'
      clonedRoot.style.overflow = 'visible'
      clonedRoot.style.width = `${width}px`
      clonedRoot.style.minWidth = `${width}px`
      clonedRoot.style.maxWidth = `${width}px`
      clonedRoot.style.boxSizing = 'border-box'

      // Un-clip vertical scrolling so all rows render.
      clonedRoot.querySelectorAll<HTMLElement>('*').forEach((node) => {
        const o = getComputedStyle(node)
        if (o.overflowY !== 'visible') node.style.overflowY = 'visible'
      })

      // Walk every ancestor up to <body> and remove height constraints so the
      // flex/fixed parent chain can't clip the root to the live element height.
      let ancestor = clonedRoot.parentElement
      while (ancestor && ancestor !== doc.body) {
        ancestor.style.height = `${height}px`
        ancestor.style.minHeight = 'unset'
        ancestor.style.maxHeight = 'unset'
        ancestor.style.overflow = 'visible'
        // In fullscreen the .wrapper is position:fixed — reset to static so
        // html2canvas doesn't offset the capture from the document origin.
        if (getComputedStyle(ancestor).position === 'fixed') {
          ancestor.style.position = 'static'
          ancestor.style.inset = 'unset'
          ancestor.style.zIndex = 'unset'
          ancestor.style.width = `${width}px`
        }
        ancestor = ancestor.parentElement
      }

      // Re-pin the group-label column to its live width so the clone wraps labels
      // exactly like the on-screen view (the Splitter's JS-computed widths are
      // lost in the clone). Pin both the pane and its Splitter.Panel ancestor.
      if (groupPaneWidth && groupPaneWidth > 0) {
        const pane = clonedRoot.querySelector<HTMLElement>(
          '[data-timeline-group-pane]',
        )
        if (pane) {
          const fix = (el: HTMLElement) => {
            el.style.width = `${groupPaneWidth}px`
            el.style.minWidth = `${groupPaneWidth}px`
            el.style.maxWidth = `${groupPaneWidth}px`
            el.style.flex = `0 0 ${groupPaneWidth}px`
          }
          fix(pane)
          // The antd Splitter.Panel wrapper is the pane's parent.
          if (pane.parentElement) fix(pane.parentElement)
        }
      }
    },
  })

  // If the consumer supplied a footer (e.g. a color legend rendered outside the
  // chart), capture it separately and stitch it beneath the chart. Constrain the
  // clone to the chart width so its contents WRAP to that width (matching the
  // on-screen layout) instead of running wide and padding blank margin on the
  // right. The footer height is whatever the wrapped content needs.
  const footerCanvas = options.footer
    ? await html2canvas(options.footer, {
        backgroundColor: options.backgroundColor ?? null,
        scale,
        width,
        windowWidth: width,
        scrollY: -window.scrollY,
        scrollX: -window.scrollX,
        onclone: (doc, cloned) => {
          // Clear body/html margin/padding to avoid layout offsets
          doc.body.style.margin = '0'
          doc.body.style.padding = '0'
          doc.documentElement.style.margin = '0'
          doc.documentElement.style.padding = '0'

          // Pin the footer to the chart width so flex/inline children wrap here.
          cloned.style.width = `${width}px`
          cloned.style.maxWidth = `${width}px`
          cloned.style.boxSizing = 'border-box'
        },
      })
    : null

  const canvas =
    footerCanvas && footerCanvas.height > 0
      ? stitchVertically(chartCanvas, footerCanvas, options.backgroundColor)
      : chartCanvas

  const link = document.createElement('a')
  link.href = canvas.toDataURL('image/png')
  link.download = options.fileName
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
}

/**
 * Stack two canvases vertically (top above bottom) onto one, left-aligned.
 * The output is exactly the CHART (top) width — the footer is clipped to it, so
 * a footer whose own canvas renders wider (html2canvas can capture an inline
 * element at the document width) never adds blank margin on the right.
 */
function stitchVertically(
  top: HTMLCanvasElement,
  bottom: HTMLCanvasElement,
  backgroundColor?: string,
): HTMLCanvasElement {
  const out = document.createElement('canvas')
  out.width = top.width
  out.height = top.height + bottom.height
  const ctx = out.getContext('2d')
  if (!ctx) return top
  if (backgroundColor) {
    ctx.fillStyle = backgroundColor
    ctx.fillRect(0, 0, out.width, out.height)
  }
  ctx.drawImage(top, 0, 0)
  // Source-crop the footer to the chart width so it never overflows to the right.
  const w = Math.min(bottom.width, top.width)
  ctx.drawImage(bottom, 0, 0, w, bottom.height, 0, top.height, w, bottom.height)
  return out
}
