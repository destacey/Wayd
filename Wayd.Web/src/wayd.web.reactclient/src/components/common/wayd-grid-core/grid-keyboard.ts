// grid-keyboard.ts — where a navigation key moves the focused body cell.
//
// Pure position math, so the key map is testable without a rendered grid. The
// grid body owns focus and applies the result; tree and group keys (expand,
// collapse, move to parent) need row objects and live there.

/** A body cell by its display position: row in the row model, column in the
 *  on-screen order of visible leaf columns (left pinned, centre, right pinned). */
export interface CellPosition {
  rowIndex: number
  colIndex: number
}

/** The extent the cursor moves within. */
export interface CellBounds {
  rowCount: number
  colCount: number
  /** Rows one PageUp/PageDown moves — what fits in the viewport. */
  pageSize: number
}

/** The modifier keys that change what a navigation key does. */
export interface NavigationModifiers {
  ctrlKey?: boolean
  metaKey?: boolean
}

const clamp = (value: number, max: number) => Math.max(0, Math.min(value, max))

/**
 * The cell a navigation key moves to, or `null` when the key does not move the
 * cursor. Movement stops at the grid's edges rather than wrapping, as a
 * spreadsheet does. Ctrl (Cmd on macOS) turns Home/End into first/last cell of
 * the grid.
 */
export function moveCell(
  key: string,
  from: CellPosition,
  bounds: CellBounds,
  modifiers: NavigationModifiers = {},
): CellPosition | null {
  if (bounds.rowCount === 0 || bounds.colCount === 0) return null
  const lastRow = bounds.rowCount - 1
  const lastCol = bounds.colCount - 1
  const page = Math.max(1, bounds.pageSize)
  const toGridEdge = !!(modifiers.ctrlKey || modifiers.metaKey)

  switch (key) {
    case 'ArrowUp':
      return {
        rowIndex: clamp(from.rowIndex - 1, lastRow),
        colIndex: from.colIndex,
      }
    case 'ArrowDown':
      return {
        rowIndex: clamp(from.rowIndex + 1, lastRow),
        colIndex: from.colIndex,
      }
    case 'ArrowLeft':
      return {
        rowIndex: from.rowIndex,
        colIndex: clamp(from.colIndex - 1, lastCol),
      }
    case 'ArrowRight':
      return {
        rowIndex: from.rowIndex,
        colIndex: clamp(from.colIndex + 1, lastCol),
      }
    case 'Home':
      return toGridEdge
        ? { rowIndex: 0, colIndex: 0 }
        : { rowIndex: from.rowIndex, colIndex: 0 }
    case 'End':
      return toGridEdge
        ? { rowIndex: lastRow, colIndex: lastCol }
        : { rowIndex: from.rowIndex, colIndex: lastCol }
    case 'PageUp':
      return {
        rowIndex: clamp(from.rowIndex - page, lastRow),
        colIndex: from.colIndex,
      }
    case 'PageDown':
      return {
        rowIndex: clamp(from.rowIndex + page, lastRow),
        colIndex: from.colIndex,
      }
    default:
      return null
  }
}

/**
 * The indexes the row virtualizer renders, with `pinnedIndex` added when it
 * falls outside the window — so the focused row keeps its DOM node (and DOM
 * focus) while it is scrolled out of view. Sorted, because the body renders
 * rows in index order and fills any gap with a spacer.
 */
export function withPinnedIndex(
  indexes: number[],
  pinnedIndex: number | null,
): number[] {
  if (
    pinnedIndex === null ||
    pinnedIndex < 0 ||
    indexes.includes(pinnedIndex)
  ) {
    return indexes
  }
  return [...indexes, pinnedIndex].sort((a, b) => a - b)
}

/**
 * Focusable content inside a cell — what F2 or Enter moves into. A cell's own
 * controls may carry `tabIndex={-1}` (the grid is a single Tab stop), so
 * those are matched too; `data-grid-cell` keeps the cell itself out.
 */
export const CELL_CONTENT_FOCUSABLE_SELECTOR =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([data-grid-cell])'
