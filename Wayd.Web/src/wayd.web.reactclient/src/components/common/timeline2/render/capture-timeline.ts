// timeline2/render/capture-timeline.ts
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

  const canvas = await html2canvas(root, {
    backgroundColor: options.backgroundColor ?? null,
    scale,
    width,
    height,
    windowWidth: width,
    windowHeight: height,
    onclone: (_doc: Document, clonedRoot: HTMLElement) => {
      // Un-clip vertical scrolling so all rows render; keep horizontal as-is so
      // only the visible time window is captured. Let the container grow tall.
      clonedRoot.style.height = `${height}px`
      clonedRoot.querySelectorAll<HTMLElement>('*').forEach((node) => {
        const o = getComputedStyle(node)
        if (o.overflowY !== 'visible') node.style.overflowY = 'visible'
      })

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

  const link = document.createElement('a')
  link.href = canvas.toDataURL('image/png')
  link.download = options.fileName
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
}
