'use client'

import { useEffect, useLayoutEffect, useRef, useState } from 'react'
import type { RowData } from '@tanstack/react-table'
import type { Row } from './index'
import { CELL_CONTENT_FOCUSABLE_SELECTOR, moveCell } from './grid-keyboard'

/** The cell holding the grid's Tab stop, by TanStack row id and column id. */
interface ActiveCell {
  rowId: string
  columnId: string
  /** Where the row was when it became active — where the Tab stop stays if
   *  the row disappears (a cancelled draft, a deleted row). */
  rowIndex: number
}

export interface GridCellNavigationOptions<T extends RowData> {
  /** The displayed row model, in display order. */
  rows: Row<T>[]
  /** Visible leaf column ids in on-screen order. */
  columnIds: string[]
  /** Row grouping is on, so some rows are group headings. */
  isGrouped: boolean
  /** The column whose cells expand and collapse tree rows with → and ←. */
  treeColumnId: string | null
  /**
   * The data id of the row in inline edit mode, if any. That row's keys
   * belong to its editors, so navigation leaves them alone.
   */
  editingRowId: string | null
  /** The data id a row is edited by (tree mode), compared with `editingRowId`. */
  getEditingId?: (row: Row<T>) => string
  /** Whether Enter on this cell should start inline editing. */
  isCellEditable?: (row: Row<T>, columnId: string) => boolean
  /** Flat-mode row activation; Enter and Space on a cell call it. */
  onRowActivate?: (row: T) => void
  /** The scrolling body viewport — sizes PageUp/PageDown. */
  bodyViewportRef: React.RefObject<HTMLElement | null>
  /** A rendered row's height in px, for the page size. */
  rowHeight: number
}

const escapeSelectorValue = (value: string) =>
  typeof CSS !== 'undefined' && CSS.escape
    ? CSS.escape(value)
    : value.replace(/["\\]/g, '\\$&')

/** The cell element for a row and column, or the row's only cell (a group
 *  heading spans the grid, so it has no column of its own). */
const findCell = (
  container: HTMLElement | null,
  rowId: string,
  columnId: string,
): HTMLElement | null => {
  const tr = container?.querySelector<HTMLElement>(
    `tr[data-grid-row="${escapeSelectorValue(rowId)}"]`,
  )
  if (!tr) return null
  return (
    tr.querySelector<HTMLElement>(
      `[data-grid-cell][data-column-id="${escapeSelectorValue(columnId)}"]`,
    ) ?? tr.querySelector<HTMLElement>('[data-grid-cell]')
  )
}

/**
 * The grid's keyboard model for body cells — a roving tab stop that arrow
 * keys, Home/End, PageUp/PageDown and the tree keys move, with Enter, F2 and
 * Space reaching into a cell (activate the row, start inline editing, or focus
 * the cell's own control).
 *
 * The body is a single Tab stop: Tab from a cell, or from a control inside
 * one, leaves the grid. That is done by moving focus to a sentinel just before
 * or after the body during keydown, so the browser's own Tab step runs from
 * there — the grid never has to compute the page's next focusable element.
 * Tabbing in from outside onto a control inside a cell (a link, a drag handle)
 * is redirected to the active cell.
 *
 * While focus is in the body, the focused row is pinned into the
 * virtualizer's window (`pinnedIndex`) so scrolling it out of view does not
 * unmount it and drop focus to `<body>`. Otherwise nothing is pinned, and the
 * body gives the Tab stop to a rendered row when the active one is not.
 */
export function useGridCellNavigation<T extends RowData>({
  rows,
  columnIds,
  isGrouped,
  treeColumnId,
  editingRowId,
  getEditingId,
  isCellEditable,
  onRowActivate,
  bodyViewportRef,
  rowHeight,
}: GridCellNavigationOptions<T>) {
  const [activeCell, setActiveCell] = useState<ActiveCell | null>(null)
  const [hasFocusWithin, setHasFocusWithin] = useState(false)
  const pendingFocusRef = useRef<ActiveCell | null>(null)
  const sentinelBeforeRef = useRef<HTMLSpanElement>(null)
  const sentinelAfterRef = useRef<HTMLSpanElement>(null)
  // Whether the user last used the keyboard or a pointer. Focus is put back
  // on a cell after inline editing ends only for keyboard users: a pointer
  // user has just clicked somewhere else, and that is where focus belongs.
  const lastInputRef = useRef<'keyboard' | 'pointer'>('pointer')
  // Set by the Menu key's keydown; the browser's own context menu follows on
  // keyup, and the grid's contextmenu handler suppresses it.
  const menuKeyRef = useRef(false)

  // When the active row is gone (filtered out, collapsed under a parent,
  // removed) the Tab stop stays where it was, on whichever row is there now.
  const foundRowIndex = activeCell
    ? rows.findIndex((row) => row.id === activeCell.rowId)
    : -1
  const activeRowIndex =
    rows.length === 0
      ? -1
      : foundRowIndex >= 0
        ? foundRowIndex
        : Math.min(activeCell?.rowIndex ?? 0, rows.length - 1)
  const activeColIndex = Math.max(
    0,
    activeCell ? columnIds.indexOf(activeCell.columnId) : 0,
  )
  const activeRowId = activeRowIndex >= 0 ? rows[activeRowIndex].id : null
  const activeColumnId = columnIds[activeColIndex] ?? null

  const isEditingRow = (row: Row<T> | undefined) =>
    !!row && !!editingRowId && getEditingId?.(row) === editingRowId

  const rowOf = (el: HTMLElement) => {
    const rowId = el.closest('tr')?.getAttribute('data-grid-row')
    return rowId ? rows.find((row) => row.id === rowId) : undefined
  }

  const focusCell = (target: ActiveCell) => {
    setActiveCell(target)
    const el = findCell(bodyViewportRef.current, target.rowId, target.columnId)
    if (el) {
      el.focus()
      return
    }
    // Not rendered yet: the state change pins its row into the window, and
    // the layout effect below focuses it once it is in the DOM.
    pendingFocusRef.current = target
  }

  useLayoutEffect(() => {
    const pending = pendingFocusRef.current
    if (!pending) return
    if (!rows.some((row) => row.id === pending.rowId)) {
      pendingFocusRef.current = null
      return
    }
    const el = findCell(
      bodyViewportRef.current,
      pending.rowId,
      pending.columnId,
    )
    if (el) {
      pendingFocusRef.current = null
      el.focus()
    }
  })

  useEffect(() => {
    const onKey = () => {
      lastInputRef.current = 'keyboard'
    }
    const onPointer = () => {
      lastInputRef.current = 'pointer'
    }
    document.addEventListener('keydown', onKey, true)
    document.addEventListener('pointerdown', onPointer, true)
    return () => {
      document.removeEventListener('keydown', onKey, true)
      document.removeEventListener('pointerdown', onPointer, true)
    }
  }, [])

  // When inline editing ends, its editors unmount and focus falls to <body>.
  // Put it back on the cell the user was editing.
  const previousEditingRowIdRef = useRef(editingRowId)
  useEffect(() => {
    const previous = previousEditingRowIdRef.current
    previousEditingRowIdRef.current = editingRowId
    if (!previous || editingRowId || lastInputRef.current !== 'keyboard') return
    const active = document.activeElement
    if (active && active !== document.body) return
    if (activeRowId && activeColumnId) {
      findCell(bodyViewportRef.current, activeRowId, activeColumnId)?.focus()
    }
  }, [editingRowId, activeRowId, activeColumnId, bodyViewportRef])

  /** Remember the cell focus landed in, however it got there (click, Tab,
   *  an editor taking focus). */
  const onFocus = (e: React.FocusEvent<HTMLElement>) => {
    const target = e.target as HTMLElement
    // React bubbles focus from portals (menus, modals) through the component
    // tree; only focus inside the body's own DOM counts.
    if (!e.currentTarget.contains(target)) return
    if (!hasFocusWithin) setHasFocusWithin(true)

    const cell = target.closest<HTMLElement>('[data-grid-cell]')
    const row = cell ? rowOf(cell) : undefined
    if (!cell || !row) return

    const from = e.relatedTarget as Node | null
    const enteredFromOutside = !from || !e.currentTarget.contains(from)
    if (
      target !== cell &&
      enteredFromOutside &&
      lastInputRef.current === 'keyboard' &&
      !isEditingRow(row)
    ) {
      // Tabbed in onto a link or drag handle: land on the Tab stop instead,
      // so the body stays one stop however many controls its rows hold.
      e.currentTarget
        .querySelector<HTMLElement>('[data-grid-cell][tabindex="0"]')
        ?.focus()
      return
    }

    const columnId = cell.getAttribute('data-column-id') ?? activeColumnId
    if (!columnId) return
    if (activeCell?.rowId === row.id && activeCell.columnId === columnId) return
    setActiveCell({
      rowId: row.id,
      columnId,
      rowIndex: rows.indexOf(row),
    })
  }

  const onBlur = (e: React.FocusEvent<HTMLElement>) => {
    const next = e.relatedTarget as Node | null
    if (next && e.currentTarget.contains(next)) return
    // The window lost focus (Alt+Tab): focus comes back to the same cell, so
    // its row must stay rendered.
    if (typeof document.hasFocus === 'function' && !document.hasFocus()) return
    pendingFocusRef.current = null
    setHasFocusWithin(false)
  }

  /** Stops the browser's context menu that follows the Menu key, which has
   *  already opened the row's own actions menu. */
  const onContextMenu = (e: React.MouseEvent<HTMLElement>) => {
    if (!menuKeyRef.current) return
    menuKeyRef.current = false
    e.preventDefault()
  }

  const pageSize = () => {
    const height = bodyViewportRef.current?.clientHeight ?? 0
    return Math.max(1, Math.floor(height / Math.max(1, rowHeight)) - 1)
  }

  /** Leave the grid: the browser's Tab step runs on from the sentinel. */
  const leaveGrid = (backwards: boolean) => {
    const sentinel = backwards
      ? sentinelBeforeRef.current
      : sentinelAfterRef.current
    sentinel?.focus({ preventScroll: true })
  }

  const onKeyDown = (e: React.KeyboardEvent<HTMLElement>) => {
    const target = e.target as HTMLElement
    // Keys from portals bubble here through the React tree; a nested grid's
    // rows could even match this one's row ids.
    if (!e.currentTarget.contains(target) || e.defaultPrevented) return
    const tr = target.closest('tr')
    const rowId = tr?.getAttribute('data-grid-row')
    if (!tr || !rowId) return
    const rowIndex = rows.findIndex((row) => row.id === rowId)
    if (rowIndex < 0) return
    const row = rows[rowIndex]

    if (isEditingRow(row)) return

    const isCell = target.hasAttribute('data-grid-cell')
    if (!isCell) {
      // Focus is on a control inside a cell: its keys are its own, except
      // Escape, which hands focus back to the cell, and Tab, which leaves
      // the grid rather than walking every other row's controls.
      if (e.key === 'Escape') {
        target.closest<HTMLElement>('[data-grid-cell]')?.focus()
      } else if (e.key === 'Tab') {
        leaveGrid(e.shiftKey)
      }
      return
    }

    const isGroupRow = isGrouped && row.getIsGrouped()
    const columnId = target.getAttribute('data-column-id') ?? activeColumnId
    const colIndex = columnId ? Math.max(0, columnIds.indexOf(columnId)) : 0

    const focusRow = (index: number, column = columnIds[colIndex]) => {
      if (column === undefined) return
      focusCell({ rowId: rows[index].id, columnId: column, rowIndex: index })
    }

    switch (e.key) {
      case 'Tab':
        leaveGrid(e.shiftKey)
        return

      case 'Enter':
      case ' ': {
        // Space would scroll the body; Enter could submit a surrounding form.
        e.preventDefault()
        if (isGroupRow) {
          row.toggleExpanded()
          return
        }
        if (onRowActivate) {
          onRowActivate(row.original)
          return
        }
        if (e.key === 'Enter') enterCell(row, target, columnId)
        return
      }

      case 'F2': {
        e.preventDefault()
        if (!isGroupRow) enterCell(row, target, columnId)
        return
      }

      case 'ContextMenu':
      case 'F10': {
        if (e.key === 'F10' && !e.shiftKey) return
        const trigger = tr.querySelector<HTMLElement>('[data-row-actions]')
        if (!trigger) return
        e.preventDefault()
        menuKeyRef.current = e.key === 'ContextMenu'
        trigger.focus()
        trigger.click()
        return
      }

      case 'ArrowRight':
        if (isGroupRow || columnId === treeColumnId) {
          if (row.getCanExpand() && !row.getIsExpanded()) {
            e.preventDefault()
            row.toggleExpanded(true)
            return
          }
        }
        break

      case 'ArrowLeft':
        if (isGroupRow || columnId === treeColumnId) {
          if (row.getCanExpand() && row.getIsExpanded()) {
            e.preventDefault()
            row.toggleExpanded(false)
            return
          }
          const parent = row.getParentRow()
          const parentIndex = parent
            ? rows.findIndex((r) => r.id === parent.id)
            : -1
          if (parentIndex >= 0) {
            e.preventDefault()
            focusRow(parentIndex)
            return
          }
        }
        break
    }

    const next = moveCell(
      e.key,
      { rowIndex, colIndex },
      {
        rowCount: rows.length,
        colCount: columnIds.length,
        pageSize: pageSize(),
      },
      { ctrlKey: e.ctrlKey, metaKey: e.metaKey },
    )
    if (!next) return
    e.preventDefault()
    focusRow(next.rowIndex, columnIds[next.colIndex])
  }

  /** Enter or F2 on a cell: start inline editing where the column is
   *  editable, otherwise move focus to the cell's own control — a link first,
   *  and never an expand toggle, which the arrow keys already drive. */
  const enterCell = (
    row: Row<T>,
    cell: HTMLElement,
    columnId: string | null,
  ) => {
    const content =
      cell.querySelector<HTMLElement>('a[href]') ??
      Array.from(
        cell.querySelectorAll<HTMLElement>(CELL_CONTENT_FOCUSABLE_SELECTOR),
      ).find((el) => !el.hasAttribute('aria-expanded'))
    const editable = !!columnId && !!isCellEditable?.(row, columnId)
    if (editable || (!content && isCellEditable)) {
      // The row's click handler is what starts inline editing at a column.
      cell.click()
      return
    }
    content?.focus()
  }

  return {
    /** Row index the virtualizer must keep rendered, or null. */
    pinnedIndex: hasFocusWithin && activeRowIndex >= 0 ? activeRowIndex : null,
    /** The row holding the Tab stop, by display index (-1 with no rows). */
    activeRowIndex,
    /** The column holding the Tab stop. */
    activeColumnId,
    onKeyDown,
    onFocus,
    onBlur,
    onContextMenu,
    sentinelBeforeRef,
    sentinelAfterRef,
  }
}
