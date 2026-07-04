/**
 * Column autosize for the Wayd grid. TanStack has no built-in autosize, so
 * this measures the RENDERED window only — under row virtualization that's
 * all the DOM that exists (ag-grid measured the same way) — and the result is
 * applied through the shared `columnSizing` state (never DOM mutation) so the
 * colgroup shared by the header and body tables updates both.
 *
 * Measuring is a clone pass: each cell's children are cloned into an
 * off-screen inline-block holder (nowrap, no width constraint), so content
 * that the live cell clips with ellipsis — or that fills the cell with
 * `width: 100%` flex wrappers — reports its intrinsic content width. All
 * holders are laid out first and measured in one pass (one reflow).
 */

/** Sane clamp defaults when a column def declares no minSize/maxSize. */
export const AUTOSIZE_MIN_WIDTH = 60
export const AUTOSIZE_MAX_WIDTH = 600

/** Horizontal box overhead around a body cell's content: td padding (7px per
 *  side) + border + a rounding buffer so ellipsis never reappears at the
 *  computed width. */
export const AUTOSIZE_CELL_ALLOWANCE = 18

/** Horizontal overhead around the header label: th + content padding, plus
 *  reserved room for the sort icon and the column menu trigger (both live in
 *  the header cell whether currently visible or not). */
export const AUTOSIZE_HEADER_ALLOWANCE = 76

export interface AutosizeWidthInput {
  /** Widest measured content among the rendered cells, px (0 = no cells). */
  maxCellContentWidth: number
  /** Measured header label width, px (0 = no measurable header). */
  headerContentWidth: number
  cellAllowance?: number
  headerAllowance?: number
  /** Column def clamps; fall back to the AUTOSIZE_MIN/MAX defaults. */
  minWidth?: number
  maxWidth?: number
}

/**
 * The width to apply for an autosized column: content + allowance, wide
 * enough for both the widest cell and the header label, clamped to the
 * column's (or the default) min/max.
 */
export function computeAutosizeWidth(input: AutosizeWidthInput): number {
  const {
    maxCellContentWidth,
    headerContentWidth,
    cellAllowance = AUTOSIZE_CELL_ALLOWANCE,
    headerAllowance = AUTOSIZE_HEADER_ALLOWANCE,
    minWidth = AUTOSIZE_MIN_WIDTH,
    maxWidth = AUTOSIZE_MAX_WIDTH,
  } = input

  const cellWidth =
    maxCellContentWidth > 0 ? maxCellContentWidth + cellAllowance : 0
  const headerWidth =
    headerContentWidth > 0 ? headerContentWidth + headerAllowance : 0
  const measured = Math.ceil(Math.max(cellWidth, headerWidth))
  return Math.min(Math.max(measured, minWidth), maxWidth)
}

export interface ColumnContentMeasurement {
  headerContentWidth: number
  maxCellContentWidth: number
}

/**
 * Measures the rendered content width of each requested column: the header
 * label (the `[data-column-header-text]` span inside the column's `th`) and
 * every rendered `td[data-column-id]` body cell. Returns raw content widths —
 * {@link computeAutosizeWidth} adds allowances and clamps.
 *
 * The measurer is appended inside the grid so clones inherit the grid's font
 * and theme variables. Browser-only by nature; in layoutless environments
 * (jsdom) every measurement is 0 and autosize falls back to the min width.
 */
export function measureColumnContent(
  headerRoot: HTMLElement,
  bodyRoot: HTMLElement,
  columnIds: string[],
): Map<string, ColumnContentMeasurement> {
  const results = new Map<string, ColumnContentMeasurement>()
  if (columnIds.length === 0) return results

  const wanted = new Set(columnIds)
  const measurer = document.createElement('div')
  measurer.style.cssText =
    'position:absolute;visibility:hidden;pointer-events:none;top:-10000px;left:0;white-space:nowrap;'

  /** Clones an element's children into a fresh inline-block holder. */
  const addHolder = (source: Element): HTMLElement => {
    const holder = document.createElement('div')
    holder.style.display = 'inline-block'
    for (const child of Array.from(source.childNodes)) {
      holder.appendChild(child.cloneNode(true))
    }
    measurer.appendChild(holder)
    return holder
  }

  // Build every holder before reading any width — one layout pass.
  const headerHolders = new Map<string, HTMLElement>()
  for (const id of columnIds) {
    const label = headerRoot.querySelector(
      `th[data-column-id="${CSS.escape(id)}"] [data-column-header-text]`,
    )
    if (label) headerHolders.set(id, addHolder(label))
  }

  const cellHolders = new Map<string, HTMLElement[]>()
  for (const cell of Array.from(
    bodyRoot.querySelectorAll('td[data-column-id]'),
  )) {
    const id = cell.getAttribute('data-column-id')
    if (!id || !wanted.has(id)) continue
    const holders = cellHolders.get(id) ?? []
    holders.push(addHolder(cell))
    cellHolders.set(id, holders)
  }

  // Attach next to the body viewport (not inside it — extra content in the
  // scroll container would perturb its scrollWidth while attached).
  const host = bodyRoot.parentElement ?? bodyRoot
  host.appendChild(measurer)
  try {
    for (const id of columnIds) {
      const headerContentWidth =
        headerHolders.get(id)?.getBoundingClientRect().width ?? 0
      let maxCellContentWidth = 0
      for (const holder of cellHolders.get(id) ?? []) {
        maxCellContentWidth = Math.max(
          maxCellContentWidth,
          holder.getBoundingClientRect().width,
        )
      }
      results.set(id, { headerContentWidth, maxCellContentWidth })
    }
  } finally {
    measurer.remove()
  }

  return results
}
