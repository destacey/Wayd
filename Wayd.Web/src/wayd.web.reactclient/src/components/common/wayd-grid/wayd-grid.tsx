'use client'

import { createPortal } from 'react-dom'
import {
  forwardRef,
  type Ref,
  type ReactElement,
  type ReactNode,
  useCallback,
  useEffect,
  useEffectEvent,
  useImperativeHandle,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
} from 'react'
import { Popover, Spin } from 'antd'
import { FilterFilled, FilterOutlined } from '@ant-design/icons'
import type { RowData } from '@tanstack/react-table'
import type {
  Column,
  ColumnDef,
  Header,
  Row,
  Table,
  VisibilityState,
} from '../wayd-grid-core'
import { defaultRangeExtractor, useVirtualizer } from '@tanstack/react-virtual'
import {
  DndContext,
  type DragCancelEvent,
  type DragEndEvent,
  type DragStartEvent,
  type Modifier,
  closestCenter,
} from '@dnd-kit/core'
import {
  SortableContext,
  arrayMove,
  horizontalListSortingStrategy,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'

import { WaydEmpty } from '@/src/components/common'
import { useRemainingHeight } from '@/src/hooks'
import {
  CombinedFilterPanel,
  DateFilterPanel,
  FilterPopup,
  FloatingFilter,
  SET_FILTER_BLANK,
  SET_FILTER_BLANK_LABEL,
  SetFilterPanel,
  canFloatingEditDate,
  describeDateFilter,
  resolveFilterType,
  toDayKey,
  waydColumnFilter,
  type ColumnFilterModel,
  type FilterType,
} from '../wayd-grid-core/filters'

import { applySafeAccessor } from '../wayd-grid-core/column-accessors'
import { virtualSpacers } from '../wayd-grid-core/virtual-geometry'
import { withPinnedIndex } from '../wayd-grid-core/grid-keyboard'
import { useGridCellNavigation } from '../wayd-grid-core/use-grid-cell-navigation'
import {
  useGridCellEditing,
  type GridCellEditingController,
} from '../wayd-grid-core/cell-editing/use-grid-cell-editing'
import { WaydGridKeyboardHelp } from '../wayd-grid-core/cell-editing/grid-keyboard-help'
import { CellEditMessages } from '../wayd-grid-core/cell-editing/cell-edit-messages'
import { GridKeyHints } from '../wayd-grid-core/cell-editing/grid-key-hints'
import type { GridRowActionSupport } from '../wayd-grid-core/cell-editing/grid-keymap'
// From the file, not the barrel: page tests mock the barrel with only
// `useMessage`, and every page that renders a grid would break.
import { useOptionalMessage } from '@/src/components/contexts/messaging/messaging-context'
import {
  getOrderedVisibleLeafColumns,
  reconcileColumnOrder,
  reorderIds,
} from '../wayd-grid-core/column-order'
import {
  computeAutosizeWidth,
  measureColumnContent,
} from '../wayd-grid-core/column-autosize'
import {
  ColumnChooserModal,
  ColumnMenuTrigger,
  getColumnChooserOptions,
} from '../wayd-grid-core/column-menu'
import {
  getPinnedBandOffsets,
  getPinnedOffsets,
  pinnedCellClassNames,
  pinnedCellStyle,
  type PinnedCellClasses,
} from '../wayd-grid-core/column-pinning'
import { applyColumnType } from '../wayd-grid-core/column-types'
import { rowsHaveId, withIdColumn } from '../wayd-grid-core/id-column'
import { useGridDndSensors } from '../wayd-grid-core/dnd/grid-dnd'
import {
  INDENTATION_WIDTH,
  calculateOrderInParent,
  defaultMoveValidator,
  getProjection,
  type TreeNode,
} from '../wayd-grid-core/dnd/tree-projection'
import {
  indentMove,
  orderAfter,
  outdentMove,
  rowActionFor,
  type RowActionKey,
} from '../wayd-grid-core/row-actions'
import {
  DRAFT_PREFIX,
  mergeDraftsIntoTree,
  type DraftItem,
} from '../wayd-grid-core/draft-utils'
import { exportGridToCsv } from '../wayd-grid-core/grid-export'
import {
  GridHeaderContent,
  SortableHeaderCell,
  useResizeClickGuard,
  type GridHeaderCellClasses,
} from '../wayd-grid-core/grid-header-row'
import {
  FlatGridRow,
  GroupGridRow,
  SortableFlatGridRow,
  TreeGridRow,
  type GroupGridRowClasses,
  type GridRowClasses,
  type TreeGridRowClasses,
} from '../wayd-grid-core/grid-row'
import {
  caseInsensitiveCompare,
  sortEmptyLast,
} from '../wayd-grid-core/grid-sorting'
import GridToolbar from '../wayd-grid-core/grid-toolbar'
import { countTreeNodes, flattenTree } from '../wayd-grid-core/tree-utils'
import { useGridColumnStatePersistence } from '../wayd-grid-core/use-grid-persistence'
import {
  mergeColumnVisibility,
  useGridState,
  useGridTable,
} from '../wayd-grid-core/use-grid-table'
import styles from './wayd-grid.module.css'
import type {
  GridColumnContext,
  GroupHeaderContext,
  WaydGridHandle,
  WaydGridProps,
} from './types'

/** Stable empty set for columns with no expanded date-tree nodes yet. Never
 * mutated — toggles always copy before writing. */
const EMPTY_NODE_SET: Set<string> = new Set<string>()

const EMPTY_DATA: never[] = []

/** Column-reorder drags move along the header row only — lock the y axis.
 *  (Inlined rather than pulling in @dnd-kit/modifiers for one function.) */
const restrictToHorizontalAxis: Modifier = ({ transform }) => ({
  ...transform,
  y: 0,
})

/** Row height used until a body row has been measured. Rows are uniform in
 * practice (.td: 4px padding + 1px border + one no-wrap text line), so one
 * measured row sizes them all. */
const ROW_HEIGHT_ESTIMATE = 28

/** Column-header row height, for sizing a `variant="simple"` grid to its rows. */
const SIMPLE_HEADER_HEIGHT = 39
/** Body height a `variant="simple"` grid keeps with no rows, so its loading
 * spinner or empty message has room to show. */
const SIMPLE_EMPTY_BODY_HEIGHT = 140
/** Rows rendered beyond the visible window on each side. Also what keeps
 * jsdom tests rendering rows at all: with no layout the viewport measures
 * 0px, so the virtualizer renders indexes 0..overscan. */
const ROW_OVERSCAN = 10

const headerCellClasses: GridHeaderCellClasses = {
  th: styles.th,
  thSortable: styles.thSortable,
  thContent: styles.thContent,
  thText: styles.thText,
  resizer: styles.resizer,
  resizerActive: styles.resizerActive,
  thDraggable: styles.thDraggable,
  thDragging: styles.thDragging,
}

// Pinned-column sticky classes per cell kind. The edge classes (the divider
// between the pinned and scrolling sections) are shared.
const headerPinnedClasses: PinnedCellClasses = {
  pinned: styles.thPinned,
  pinnedLeftEdge: styles.pinnedLeftEdge,
  pinnedRightEdge: styles.pinnedRightEdge,
}

const filterPinnedClasses: PinnedCellClasses = {
  pinned: styles.filterThPinned,
  pinnedLeftEdge: styles.pinnedLeftEdge,
  pinnedRightEdge: styles.pinnedRightEdge,
}

const rowClasses: GridRowClasses = {
  tr: styles.tr,
  trAlt: styles.trAlt,
  td: styles.td,
  tdNumeric: styles.tdNumeric,
  trActivatable: styles.trActivatable,
  trActivated: styles.trActivated,
  pinned: {
    pinned: styles.tdPinned,
    pinnedLeftEdge: styles.pinnedLeftEdge,
    pinnedRightEdge: styles.pinnedRightEdge,
  },
  edit: {
    unsaved: styles.cellUnsaved,
    saving: styles.cellSaving,
    error: styles.cellError,
    editing: styles.cellEditing,
  },
}

const treeRowClasses: TreeGridRowClasses = {
  ...rowClasses,
  treeEditorIndent: styles.treeEditorIndent,
}

const groupRowClasses: GroupGridRowClasses = {
  tr: styles.tr,
  trGroup: styles.trGroup,
  td: styles.td,
  groupCell: styles.groupCell,
  groupCellInner: styles.groupCellInner,
  groupToggle: styles.groupToggle,
  groupToggleExpanded: styles.groupToggleExpanded,
  groupLabel: styles.groupLabel,
  groupCount: styles.groupCount,
}

/**
 * Resolves a column's filter/data type. An explicit `meta.filterType`
 * (including one applied by a `columnType`) always wins; otherwise the type is
 * inferred from the column's data by sampling its faceted unique values:
 *   - all sampled values are numbers      → 'number'
 *   - all sampled values are Dates        → 'date'
 *   - otherwise                           → 'text'
 * Empty/mixed data falls back to 'text'. Drives both the per-column filter UI
 * and numeric cell alignment, so the two never disagree.
 */
const resolveColumnFilterType = <T extends RowData>(
  column: Column<T, unknown>,
): FilterType => {
  const meta = column.columnDef.meta
  if (meta?.filterType) return resolveFilterType(meta.filterType)

  // Display columns (actions, checkboxes — no accessor) have no values to
  // sample; TanStack's faceted-values util throws on them.
  if (!column.accessorFn) return 'text'

  const sample: unknown[] = []
  for (const v of column.getFacetedUniqueValues().keys()) {
    if (v === null || v === undefined || v === '') continue
    sample.push(v)
    if (sample.length >= 20) break // enough to infer; avoid scanning huge sets
  }
  if (sample.length === 0) return 'text'

  if (sample.every((v) => typeof v === 'number')) return 'number'
  if (sample.every((v) => v instanceof Date)) return 'date'
  return 'text'
}

interface GridBodyProps<T extends RowData> {
  rows: Row<T>[]
  /** The grid's single scroll viewport ref — the table wrapper (both axes). Its
   *  scrollLeft mirrors into the header; its scrollTop drives the bar track. */
  bodyViewportRef: React.RefObject<HTMLDivElement | null>
  /** Mirrors scrollLeft into the header viewport + scrollTop into the bar track. */
  onBodyScroll: (e: React.UIEvent<HTMLDivElement>) => void
  /** Optional right chart pane (Gantt). When set, the body wraps the table and
   *  a bar track in a shared vertical scroller split by a divider. */
  rightPane?: import('./types').WaydGridRightPane<T>
  /** Current bar-pane width, px. */
  rightPaneWidth?: number
  /** Pointer-down on the resize divider (starts a width drag). */
  onRightPaneDividerDown?: (e: React.PointerEvent) => void
  /** Ref to the bar-track element (width mutated directly during divider drag). */
  rightPaneTrackRef?: React.RefObject<HTMLDivElement | null>
  /** Ref to the bar canvas — translated vertically to follow the grid scroll. */
  rightPaneCanvasRef?: React.RefObject<HTMLDivElement | null>
  /** Mirrors the bar track's horizontal scroll into the axis header. */
  onBarTrackScroll?: (e: React.UIEvent<HTMLDivElement>) => void
  /** The shared colgroup element (same instance the header table renders).
   *  Stable across scrolls, so React skips reconciling it. */
  colGroup: ReactNode
  isLoading: boolean
  emptyMessage: string
  visibleColumnCount: number
  /** Visible leaf column ids in on-screen order — what arrow keys walk. */
  columnIds: string[]
  /** Header rows above the body (group bands, leaf headers, filters), so body
   *  rows can state their place in the whole grid. */
  headerRowCount: number
  /** The column whose cells expand and collapse tree rows with → and ←. */
  treeColumnId: string | null
  /** Column ids whose body cells right-align (numeric columns). */
  numericColumnIds: ReadonlySet<string>
  isTree: boolean
  /** Row grouping is on; see WaydGridProps.grouping. */
  isGrouped: boolean
  renderGroupHeader?: (context: GroupHeaderContext<T>) => ReactNode
  flatDndEnabled: boolean
  isDragEnabled: boolean
  draggedNodeId: string | null
  flatRowId: (row: Row<T>) => string
  /** Flat-mode row activation; see WaydGridProps.onRowActivate. */
  onRowActivate?: (row: T) => void
  activatedRowId?: string | null
  getRowActivateLabel?: (row: T) => string
  /** Cell editing, when the grid has `onCommit`. */
  cellEditing?: GridCellEditingController<T>
  /**
   * Runs a row-action key on a row. False: the grid does not do it here, so
   * the key goes on to navigation. A promise: a move, resolving to the row's
   * new parent, or false when it did not move.
   */
  runRowAction?: (
    action: RowActionKey,
    row: Row<T>,
  ) => boolean | Promise<false | { parentId: string | null }>
  /** The row actions the grid supports, for the key hints. */
  rowActionSupport?: GridRowActionSupport
  /** Where in the grid's footer the key hints render. */
  footerHintsHost?: HTMLElement | null
  /** The height a body row renders at, once measured; null before. */
  measuredRowHeight: number | null
  /** Reports a body row's rendered height. */
  onRowHeightMeasured: (height: number) => void
}

/**
 * The scrolling body viewport, split from the grid so it alone owns the row
 * virtualizer: every overscan-window shift re-renders whichever component
 * holds the virtualizer, and when that was the whole grid the header's antd
 * controls (filter popovers, floating-filter inputs) re-rendered per shift —
 * visible as scroll stutter. Confined here, a shift re-renders only the
 * ~viewport of body rows; the header, toolbar, and filters stay untouched.
 */
function GridBody<T extends RowData>({
  rows,
  bodyViewportRef,
  onBodyScroll,
  rightPane,
  rightPaneWidth,
  onRightPaneDividerDown,
  rightPaneTrackRef,
  rightPaneCanvasRef,
  onBarTrackScroll,
  colGroup,
  isLoading,
  emptyMessage,
  visibleColumnCount,
  columnIds,
  headerRowCount,
  treeColumnId,
  numericColumnIds,
  isTree,
  isGrouped,
  renderGroupHeader,
  flatDndEnabled,
  isDragEnabled,
  draggedNodeId,
  flatRowId,
  onRowActivate,
  activatedRowId,
  getRowActivateLabel,
  cellEditing,
  runRowAction,
  rowActionSupport,
  footerHintsHost,
  measuredRowHeight,
  onRowHeightMeasured,
}: GridBodyProps<T>) {
  // Owns virtualizer + TanStack row JSX — same staleness hazard as the grid.

  // Rows render at a content-driven height (29px against the 28px estimate is
  // typical). The virtualizer sizes rows at the measured height; on the
  // estimate, scroll-to-row targets and chart bars fall a pixel short per row.
  const rowHeight = measuredRowHeight ?? ROW_HEIGHT_ESTIMATE

  const navigation = useGridCellNavigation({
    rows,
    columnIds,
    isGrouped,
    treeColumnId,
    onRowActivate,
    bodyViewportRef,
    rowHeight,
  })

  // The row with an open editor stays rendered wherever focus is — even in
  // the editor's own popup, which lives outside the body.
  const openCell = cellEditing?.open
  const openRowIndex = openCell
    ? rows.findIndex(
        (row) => cellEditing.rowKeyOf(row.original) === openCell.rowKey,
      )
    : -1
  const pinnedIndex = openRowIndex >= 0 ? openRowIndex : navigation.pinnedIndex
  // A failed cell shows its message while it holds focus.
  const errorTipRowIndex =
    cellEditing && !openCell && navigation.hasFocusWithin
      ? navigation.activeRowIndex
      : -1

  /** Row-action keys on a focused cell come before navigation's; keys in a
   *  control (an open editor's Ctrl+Delete) stay the control's. */
  const onBodyKeyDown = (e: React.KeyboardEvent<HTMLElement>) => {
    const target = e.target as HTMLElement
    const action =
      runRowAction &&
      e.currentTarget.contains(target) &&
      !e.defaultPrevented &&
      target.hasAttribute('data-grid-cell')
        ? rowActionFor(e)
        : null
    const rowId = target.closest('tr')?.getAttribute('data-grid-row')
    const row = action ? rows.find((r) => r.id === rowId) : undefined
    if (action && row && !row.getIsGrouped()) {
      const result = runRowAction!(action, row)
      // A row-action key never falls through to navigation, whose Enter and
      // arrows ignore modifiers (Ctrl+Enter would edit the cell).
      e.preventDefault()
      if (result !== false) {
        const columnId = target.getAttribute('data-column-id')
        const rowKey = cellEditing?.rowKeyOf(row.original)
        if (result instanceof Promise && cellEditing && rowKey && columnId) {
          // The moved row renders elsewhere once the data shows the move;
          // focus goes with it.
          void result.then((moved) => {
            if (moved)
              cellEditing.requestFocus(rowKey, columnId, moved.parentId)
          })
        }
      }
      return
    }
    navigation.onKeyDown(e)
  }

  // A cell asked to open (a new draft) or to take focus (the row a draft
  // became) once its row is in the row model.
  useLayoutEffect(() => {
    if (!cellEditing) return
    const findRow = (rowKey: string) =>
      rows.find((row) => cellEditing.rowKeyOf(row.original) === rowKey)
    const pending = cellEditing.pendingOpen
    const pendingRow = pending ? findRow(pending.rowKey) : undefined
    if (pending && pendingRow) {
      cellEditing.clearPendingOpen()
      navigation.focusCellAt(pendingRow.id, pending.columnId)
      cellEditing.openCell(pendingRow, pending.columnId)
    }
    const focusTarget = cellEditing.pendingFocus
    const candidate = focusTarget ? findRow(focusTarget.rowKey) : undefined
    const candidateParent = candidate?.getParentRow()
    const focusRow =
      candidate &&
      (focusTarget?.parentKey === undefined ||
        (candidateParent
          ? cellEditing.rowKeyOf(candidateParent.original)
          : null) === focusTarget.parentKey)
        ? candidate
        : undefined
    if (focusTarget && focusRow) {
      cellEditing.clearPendingFocus()
      const active = document.activeElement
      // Only while the user is still in the grid and not editing another
      // cell; elsewhere it would steal focus from wherever they went.
      if (
        !cellEditing.open &&
        (!active ||
          active === document.body ||
          bodyViewportRef.current?.contains(active))
      ) {
        navigation.focusCellAt(focusRow.id, focusTarget.columnId)
      }
    }
  })

  // ─── Row virtualization ──────────────────────────────────
  // Only the visible window of rows (plus overscan) is mounted, ag-grid
  // style. The virtualizer windows the already-flattened row model, so tree
  // expand/collapse flows through naturally as a count change. Offsets render
  // as spacer rows (real <tr>s) so the body stays a genuine table — colgroup
  // column alignment with the header table is untouched.
  // eslint-disable-next-line react-hooks/incompatible-library -- the rule flags TanStack Virtual as compiler-incompatible; the virtualizer is read through its own hook each render, so there is no cached instance to go stale
  const rowVirtualizer = useVirtualizer({
    count: rows.length,
    // The focused row stays rendered while scrolled away, so it keeps DOM
    // focus; the body fills the gap around it with a spacer row.
    rangeExtractor: (range) =>
      withPinnedIndex(defaultRangeExtractor(range), pinnedIndex),
    // The grid's own table wrapper is the single vertical scroller (unchanged
    // from the no-pane case, so scroll performance is identical to baseline).
    // When a right (chart) pane is present the bar track FOLLOWS this scroller's
    // scrollTop — one virtualizer drives both, so bars can't drift from rows.
    getScrollElement: () => bodyViewportRef.current,
    estimateSize: () => rowHeight,
    overscan: ROW_OVERSCAN,
  })
  const virtualRows = rowVirtualizer.getVirtualItems()
  const totalSize = rowVirtualizer.getTotalSize()
  const hasRenderedRows = virtualRows.length > 0

  // The virtualizer caches row sizes; a new measured height must replace them.
  useLayoutEffect(() => {
    if (measuredRowHeight !== null) rowVirtualizer.measure()
  }, [measuredRowHeight, rowVirtualizer])

  // Bring a newly opened editor into view (a draft added off screen).
  useEffect(() => {
    if (openRowIndex >= 0) rowVirtualizer.scrollToIndex(openRowIndex)
  }, [openRowIndex, rowVirtualizer])

  // The virtualizer renders no rows until it has read the viewport, so the
  // first rows are measured on the render that brings them.
  useLayoutEffect(() => {
    const viewport = bodyViewportRef.current
    if (!viewport) return
    const measure = () => {
      const tr = viewport.querySelector<HTMLTableRowElement>(
        'tbody tr:not([aria-hidden])',
      )
      const h = tr?.getBoundingClientRect().height
      if (h && h > 0) onRowHeightMeasured(h)
    }
    measure()
    if (typeof ResizeObserver === 'undefined') return
    const observer = new ResizeObserver(measure)
    observer.observe(viewport)
    return () => observer.disconnect()
  }, [rows.length, hasRenderedRows, bodyViewportRef, onRowHeightMeasured])

  // The Tab stop must be on a rendered cell or Tab skips the body. When the
  // active row has scrolled away (and so is not pinned), the first row in
  // view takes it — not one in the overscan above, which Tab would scroll to.
  const firstInView = rowVirtualizer.scrollOffset ?? 0
  const tabStopRowIndex = virtualRows.some(
    (virtualRow) => virtualRow.index === navigation.activeRowIndex,
  )
    ? navigation.activeRowIndex
    : ((
        virtualRows.find((virtualRow) => virtualRow.start >= firstInView) ??
        virtualRows[0]
      )?.index ?? -1)

  const { top: spacerTop, bottom: spacerBottom } = virtualSpacers({
    firstRowStart: virtualRows[0]?.start ?? 0,
    lastRowEnd: virtualRows[virtualRows.length - 1]?.end ?? 0,
    totalSize: rowVirtualizer.getTotalSize(),
    hasRows: virtualRows.length > 0,
  })

  // While loading (or with no rows) the rows are replaced by a status
  // overlay. The overlay is a sibling of the scrolling wrapper — anchored to
  // the VISIBLE body viewport, not the table: a wide table in a narrow window
  // centers a spanning status <td> at half the scroll width, which can sit
  // entirely off-screen.
  const showStatusOverlay = isLoading || rows.length === 0

  // The grid's table wrapper — the single vertical + horizontal scroller. This
  // is unchanged whether or not a right (chart) pane is present, so scroll
  // performance matches the baseline grid exactly; the bar track follows this
  // element's scrollTop rather than sharing a different scroller.
  const tableWrapperEl = (
    <div
      className={
        cellEditing
          ? `${styles.tableWrapper} ${styles.editsByCell}`
          : styles.tableWrapper
      }
      ref={bodyViewportRef}
      onScroll={onBodyScroll}
      // Measurement hook for jsdom tests: layoutless environments report a
      // 0×0 rect here, which makes the row virtualizer render nothing —
      // vitest.setup.dom.ts returns a fixed rect for this attribute instead.
      data-grid-body-viewport=""
    >
      {/* Tab from a body cell leaves the grid by focusing one of these first;
          the browser's own Tab step then continues from it. */}
      <span
        ref={navigation.sentinelBeforeRef}
        tabIndex={-1}
        aria-hidden="true"
        className={styles.focusSentinel}
      />
      <table
        className={styles.tableElement}
        role="presentation"
        onKeyDownCapture={
          cellEditing
            ? (e) =>
                cellEditing.onKeyDownCapture(e, {
                  rows,
                  columnIds,
                  focusCell: navigation.focusCellAt,
                  leaveGrid: navigation.leaveGrid,
                })
            : undefined
        }
        onKeyDown={onBodyKeyDown}
        onFocus={navigation.onFocus}
        onBlur={navigation.onBlur}
        onContextMenu={navigation.onContextMenu}
        onPointerDownCapture={cellEditing?.onPointerDownCapture}
        onClick={cellEditing ? (e) => cellEditing.onClick(e, rows) : undefined}
        onDoubleClick={
          cellEditing ? (e) => cellEditing.onDoubleClick(e, rows) : undefined
        }
      >
        {colGroup}
        <tbody role="rowgroup">
          {!showStatusOverlay && (
            <>
              {/* Virtual offset spacers: real table rows standing in for the
                  unrendered rows above/below the window. Zero padding/border
                  so they add no width — header/body scrollWidth must match. */}
              {spacerTop > 0 && (
                <tr aria-hidden="true">
                  <td
                    className={styles.virtualSpacer}
                    colSpan={visibleColumnCount + 1}
                    style={{ height: spacerTop }}
                  />
                </tr>
              )}
              {/* flatMap, not map: a row comes back with its gap spacer,
                  and React keys a nested array by position — so as the
                  window shifted, every row would land in another slot and
                  remount, dropping focus. */}
              {virtualRows.flatMap((virtualRow, position) => {
                const row = rows[virtualRow.index]
                // Zebra striping keys off the absolute display index so
                // stripes don't shift as the window moves.
                const index = virtualRow.index
                const navigationProps = {
                  ariaRowIndex: headerRowCount + index + 1,
                  focusColumnId:
                    index === tabStopRowIndex
                      ? navigation.activeColumnId
                      : null,
                }
                // The pinned focused row can sit apart from the window; a
                // spacer stands in for the rows between them.
                const previous = virtualRows[position - 1]
                const gap = previous ? virtualRow.start - previous.end : 0
                // Edit state for this row's cells, when the grid edits.
                const rowKey = cellEditing?.rowKeyOf(row.original)
                const isOpenRow = !!rowKey && openCell?.rowKey === rowKey
                const editProps =
                  cellEditing && rowKey
                    ? {
                        cellMarks: cellEditing.marksByRow.get(rowKey),
                        editColumnId: isOpenRow ? openCell!.columnId : null,
                        editor: isOpenRow ? cellEditing.editorElement : null,
                        errorTipColumnId:
                          index === errorTipRowIndex
                            ? navigation.activeColumnId
                            : null,
                        editIdPrefix: cellEditing.idPrefix,
                        editRowKey: rowKey,
                      }
                    : undefined
                const gapSpacer =
                  gap > 0 ? (
                    <tr key={`${row.id}-gap`} aria-hidden="true">
                      <td
                        className={styles.virtualSpacer}
                        colSpan={visibleColumnCount + 1}
                        style={{ height: gap }}
                      />
                    </tr>
                  ) : null

                if (isTree) {
                  const nodeId = (row.original as { id: string }).id
                  return [
                    gapSpacer,
                    <TreeGridRow
                      key={row.id}
                      row={row}
                      index={index}
                      {...navigationProps}
                      classes={treeRowClasses}
                      numericColumnIds={numericColumnIds}
                      nodeId={nodeId}
                      isExpanded={row.getIsExpanded()}
                      isDragging={draggedNodeId === nodeId}
                      isDragEnabled={
                        isDragEnabled && !nodeId.startsWith(DRAFT_PREFIX)
                      }
                      {...editProps}
                    />,
                  ]
                }

                // A group heading spans the grid and only expands or
                // collapses; the rows under it are ordinary flat rows.
                if (isGrouped && row.getIsGrouped()) {
                  const columnId = row.groupingColumnId ?? ''
                  return [
                    gapSpacer,
                    <GroupGridRow
                      key={row.id}
                      row={row}
                      isExpanded={row.getIsExpanded()}
                      {...navigationProps}
                      classes={groupRowClasses}
                      colSpan={visibleColumnCount + 1}
                      header={renderGroupHeader?.({
                        columnId,
                        value: row.groupingValue,
                        leafRows: row
                          .getLeafRows()
                          .map((leaf) => leaf.original),
                        depth: row.depth,
                      })}
                    />,
                  ]
                }

                // Both flat forms take the same activation set, resolved
                // once so the plain and sortable rows cannot disagree.
                const activation = onRowActivate
                  ? {
                      onActivate: () => onRowActivate(row.original),
                      isActivated: flatRowId(row) === activatedRowId,
                      activateLabel: getRowActivateLabel?.(row.original),
                    }
                  : undefined

                if (flatDndEnabled) {
                  const nodeId = flatRowId(row)
                  return [
                    gapSpacer,
                    <SortableFlatGridRow
                      key={row.id}
                      row={row}
                      index={index}
                      {...navigationProps}
                      classes={rowClasses}
                      numericColumnIds={numericColumnIds}
                      nodeId={nodeId}
                      isDragging={draggedNodeId === nodeId}
                      isDragEnabled={isDragEnabled}
                      {...activation}
                      {...editProps}
                    />,
                  ]
                }

                return [
                  gapSpacer,
                  <FlatGridRow
                    key={row.id}
                    row={row}
                    index={index}
                    {...navigationProps}
                    classes={rowClasses}
                    numericColumnIds={numericColumnIds}
                    {...activation}
                    {...editProps}
                  />,
                ]
              })}
              {spacerBottom > 0 && (
                <tr aria-hidden="true">
                  <td
                    className={styles.virtualSpacer}
                    colSpan={visibleColumnCount + 1}
                    style={{ height: spacerBottom }}
                  />
                </tr>
              )}
            </>
          )}
        </tbody>
      </table>
      <span
        ref={navigation.sentinelAfterRef}
        tabIndex={-1}
        aria-hidden="true"
        className={styles.focusSentinel}
      />
      {cellEditing && (
        <CellEditMessages
          marksByRow={cellEditing.marksByRow}
          idPrefix={cellEditing.idPrefix}
          announcement={cellEditing.announcement}
        />
      )}
    </div>
  )

  // A row of the grid, so the loading or empty message is not content the
  // grid role cannot own.
  const statusOverlayEl = showStatusOverlay ? (
    <div className={styles.statusOverlay} role="row">
      <div role="gridcell">
        {isLoading ? (
          <Spin size="large" />
        ) : (
          <WaydEmpty message={emptyMessage} />
        )}
      </div>
    </div>
  ) : null

  // The keys for what the user is doing, while a cell is selected or edited.
  const activeRow =
    navigation.activeRowIndex >= 0 ? rows[navigation.activeRowIndex] : undefined
  // Rendered from here, where the selection is known, into the footer.
  const keyHintsEl =
    cellEditing &&
    footerHintsHost &&
    !showStatusOverlay &&
    (navigation.hasFocusWithin || cellEditing.open)
      ? createPortal(
          <GridKeyHints
            mode={cellEditing.openHintMode ?? 'cell'}
            cellEditable={
              !!activeRow &&
              !!navigation.activeColumnId &&
              cellEditing.isCellEditable(activeRow, navigation.activeColumnId)
            }
            rows={rowActionSupport ?? {}}
          />,
          footerHintsHost,
        )
      : null

  // ─── Default layout: the table wrapper IS the single scroller ────────────
  if (!rightPane) {
    return (
      <div className={styles.bodyArea}>
        {tableWrapperEl}
        {statusOverlayEl}
        {keyHintsEl}
      </div>
    )
  }

  // ─── Right chart pane: bar track FOLLOWS the grid's own scroller ──────────
  // The grid table keeps its original, untouched scroller (fast). The bar track
  // clips (overflow hidden) and its canvas is translated by the grid's scrollTop
  // (set in onBodyScroll, rAF-throttled) so bars track their rows. One
  // virtualizer drives both — the bar track renders the SAME virtualRows — so
  // geometry can't drift; only a scroll position is followed. The bar track owns
  // its horizontal (time-axis) scroll independently.
  const barTrack = (
    <div
      ref={rightPaneTrackRef}
      // Marks the chart viewport so a consumer can measure its live width —
      // the divider resizes it via direct DOM writes during a drag, so state
      // alone does not describe what is on screen.
      data-wayd-grid-chart-pane
      // The chart draws the rows' dates again for the eye, and is driven by
      // pointer only; inside the grid role it would be content that is not
      // a row.
      aria-hidden="true"
      // Browsers make a scrolling element a Tab stop; this one is hidden from
      // assistive tech, so it must not take keyboard focus either.
      tabIndex={-1}
      className={styles.rightPaneTrack}
      style={{ width: rightPaneWidth }}
      onScroll={onBarTrackScroll}
    >
      <div ref={rightPaneCanvasRef} className={styles.rightPaneCanvas}>
        {/* The virtualizer sizes rows at their measured height, so its
            geometry is the grid rows' own and bars sit on their rows. */}
        <div
          className={styles.rightPaneCanvasInner}
          style={{ height: totalSize }}
        >
          {/* Chart-wide layer behind rows (e.g. vertical gridlines). */}
          {!showStatusOverlay &&
            rightPane.renderBackground?.({ totalHeight: totalSize })}
          {!showStatusOverlay &&
            virtualRows.map((virtualRow) => (
              <div key={rows[virtualRow.index].id} data-gantt-row="">
                {rightPane.renderRow({
                  row: rows[virtualRow.index],
                  top: virtualRow.start,
                  height: virtualRow.size,
                })}
              </div>
            ))}
        </div>
      </div>
    </div>
  )

  return (
    <div className={styles.bodyArea}>
      <div className={styles.rightPaneLayout}>
        {tableWrapperEl}
        <div
          className={styles.rightPaneDivider}
          aria-hidden="true"
          role="separator"
          aria-orientation="vertical"
          aria-label="Resize chart pane"
          onPointerDown={onRightPaneDividerDown}
        />
        {barTrack}
      </div>
      {statusOverlayEl}
      {keyHintsEl}
    </div>
  )
}

function WaydGridInner<T extends RowData>(
  props: WaydGridProps<T>,
  ref: Ref<WaydGridHandle>,
) {
  const {
    data: dataProp,
    columns: columnsProp,
    isLoading = false,
    onRefresh,
    leftSlot,
    helpContent,
    helpNotes,
    actionsSlot,
    rightSlot,
    emptyMessage = 'No records found',
    csvFileName = 'grid-export',
    includeIdColumn = true,
    height,
    initialSorting,
    variant = 'advanced',
    includeGlobalSearch = variant === 'advanced',
    includeExportButton = variant === 'advanced',
    includeColumnFilters = variant === 'advanced',
    includeFloatingFilters = variant === 'advanced',
    getRowId,
    persistStateKey,
    onDisplayedRowsChange,
    rowSelection,
    onRowSelectionChange,
    onRowActivate,
    activatedRowId,
    getRowActivateLabel,
    onRowReorder,
    grouping,
    renderGroupHeader,
    initialGroupsExpanded = true,
    getSubRows,
    initialExpanded = true,
    enableDragAndDrop = false,
    onNodeMove,
    onMoveRejected,
    moveValidator,
    onCommit,
    onCreate,
    fieldToColumn,
    createDraftNode,
    onDraftCancelled,
    rowActions,
    rightPane,
    'aria-label': ariaLabel,
  } = props

  // Undefined data (a query hook still loading) renders as an empty grid —
  // the old ag-grid WaydGrid tolerated undefined rowData the same way, and
  // TanStack's row model throws on it. Stable fallback so the table's data
  // identity doesn't churn while loading.
  const data = dataProp ?? (EMPTY_DATA as T[])

  const isTree = !!getSubRows
  // Grouping is a flat-mode arrangement: a tree already has its hierarchy.
  const isGrouped = !isTree && !!grouping && grouping.length > 0

  // ─── Auto-height ─────────────────────────────────────────
  const [gridContainerRef, autoHeight] = useRemainingHeight()
  // A simple grid sits inside a section, so it fits its rows rather than
  // filling the remaining viewport — otherwise a four-row table runs to the
  // bottom of the page with empty space under it.
  const isSimple = variant === 'simple'
  // Sized from `data` rather than the table's row model, which is built later
  // in this component. Equivalent for a simple grid: filtering is off, so no
  // row is ever hidden.
  const [measuredRowHeight, setMeasuredRowHeight] = useState<number | null>(
    null,
  )
  const simpleBodyHeight = data?.length
    ? data.length * (measuredRowHeight ?? ROW_HEIGHT_ESTIMATE)
    : SIMPLE_EMPTY_BODY_HEIGHT
  const simpleHeight = SIMPLE_HEADER_HEIGHT + simpleBodyHeight + 2
  const resolvedHeight = height ?? (isSimple ? simpleHeight : autoHeight)

  // ─── Split header/body viewports ─────────────────────────
  // The header lives in its own clipped viewport above the scrolling body, so
  // the vertical scrollbar spans only the rows (ag-grid style). The body's
  // horizontal scroll is mirrored into the header, and a spacer as wide as the
  // body's vertical scrollbar keeps the two tables' columns aligned.
  const headerViewportRef = useRef<HTMLDivElement>(null)
  const bodyViewportRef = useRef<HTMLDivElement>(null)
  // The right-pane (Gantt) axis header viewport — mirrors the bar track's
  // horizontal scroll so the date axis tracks the bars.
  const rightPaneHeaderRef = useRef<HTMLDivElement>(null)
  // The bar-track pane element — width is mutated directly during a divider drag
  // (see onRightPaneDividerDown) so dragging doesn't re-render the whole grid.
  const rightPaneTrackRef = useRef<HTMLDivElement>(null)
  // The bar canvas — translated vertically to follow the grid's scrollTop so the
  // bars track their rows without the chart owning a second vertical scroller.
  const rightPaneCanvasRef = useRef<HTMLDivElement>(null)
  const [scrollbarWidth, setScrollbarWidth] = useState(0)

  const handleBodyScroll = useCallback((e: React.UIEvent<HTMLDivElement>) => {
    if (headerViewportRef.current) {
      headerViewportRef.current.scrollLeft = e.currentTarget.scrollLeft
    }
    // Follow the grid's vertical scroll in the bar canvas (chart pane). A single
    // transform write is cheap and must be in-frame so bars don't lag their rows.
    if (rightPaneCanvasRef.current) {
      rightPaneCanvasRef.current.style.transform = `translateY(${-e.currentTarget.scrollTop}px)`
    }
  }, [])

  // The bar track's horizontal scroll mirrors into the axis header so the date
  // axis stays aligned above the bars (same pattern as the grid header sync).
  // rAF-throttled: writing scrollLeft straight from the scroll event forces a
  // synchronous layout every frame; batching to one write per frame keeps the
  // chart's horizontal scroll smooth.
  const barTrackScrollRafRef = useRef<number | null>(null)
  const barTrackScrollLeftRef = useRef(0)
  const handleBarTrackScroll = useCallback(
    (e: React.UIEvent<HTMLDivElement>) => {
      // Record the LATEST scrollLeft each event; the rAF applies the most recent
      // value so fast/inertial scrolls don't leave the header a frame behind.
      barTrackScrollLeftRef.current = e.currentTarget.scrollLeft
      if (barTrackScrollRafRef.current != null) return
      barTrackScrollRafRef.current = requestAnimationFrame(() => {
        barTrackScrollRafRef.current = null
        if (rightPaneHeaderRef.current) {
          rightPaneHeaderRef.current.scrollLeft = barTrackScrollLeftRef.current
        }
      })
    },
    [],
  )

  const hasRightPane = !!rightPane

  // The bar track clips vertically (the canvas is translated to follow the grid),
  // so a wheel over the chart has nothing to scroll there. A NON-PASSIVE native
  // wheel listener lets the consumer's onWheel call preventDefault (e.g. to block
  // the browser's Ctrl/Cmd+wheel page zoom) — React's onWheel is passive and
  // cannot. When the consumer doesn't handle it, forward the vertical delta to
  // the grid's scroller so vertical scrolling works over the chart too.
  //
  // The consumer's onWheel is read through an effect event so the (non-passive)
  // listener stays stable across renders — consumers commonly pass an inline
  // callback, which would otherwise tear down and re-add the listener every
  // render (including mid-drag).
  const onRightPaneWheel = useEffectEvent((e: WheelEvent) =>
    rightPane?.onWheel?.(e),
  )
  useEffect(() => {
    const el = rightPaneTrackRef.current
    if (!el) return
    const onWheel = (e: WheelEvent) => {
      if (onRightPaneWheel(e) === true) return
      if (e.deltaY !== 0 && bodyViewportRef.current) {
        // Forward vertical wheel to the grid's scroller; preventDefault so the
        // browser doesn't also scroll an ancestor (e.g. the page) at the edge.
        e.preventDefault()
        bodyViewportRef.current.scrollTop += e.deltaY
      }
    }
    el.addEventListener('wheel', onWheel, { passive: false })
    return () => el.removeEventListener('wheel', onWheel)
  }, [hasRightPane])

  // Right-pane width (chart pane). Seeded from the config default; the consumer
  // persists it via onWidthChange when a drag ends.
  const [rightPaneWidth, setRightPaneWidth] = useState(
    rightPane?.defaultWidth ?? 480,
  )
  const rightPaneDragRef = useRef<{
    startX: number
    startWidth: number
    min: number
    latest: number
  } | null>(null)
  const onRightPaneDividerDown = useCallback(
    (e: React.PointerEvent) => {
      e.preventDefault()
      const drag = {
        startX: e.clientX,
        startWidth: rightPaneWidth,
        min: rightPane?.minWidth ?? 200,
        latest: rightPaneWidth,
      }
      rightPaneDragRef.current = drag
      // During the drag, mutate the pane + axis widths DIRECTLY (no React state)
      // so a per-frame resize doesn't re-render the whole grid — commit once on
      // release. rAF-throttle the DOM writes to one per frame.
      let rafId: number | null = null
      const applyWidth = (w: number) => {
        if (rightPaneTrackRef.current)
          rightPaneTrackRef.current.style.width = `${w}px`
        if (rightPaneHeaderRef.current)
          rightPaneHeaderRef.current.style.width = `${w}px`
      }
      const onMove = (ev: PointerEvent) => {
        const d = rightPaneDragRef.current
        if (!d) return
        // Dragging the divider LEFT widens the bar pane (it sits on the right).
        const next = Math.max(d.min, d.startWidth - (ev.clientX - d.startX))
        d.latest = next
        if (rafId == null) {
          rafId = requestAnimationFrame(() => {
            rafId = null
            applyWidth(d.latest)
          })
        }
      }
      const onUp = () => {
        window.removeEventListener('pointermove', onMove)
        window.removeEventListener('pointerup', onUp)
        if (rafId != null) cancelAnimationFrame(rafId)
        const finalWidth = rightPaneDragRef.current?.latest
        rightPaneDragRef.current = null
        // Commit to state once (keeps the DOM width authoritative) + persist.
        if (finalWidth != null) {
          setRightPaneWidth(finalWidth)
          rightPane?.onWidthChange?.(finalWidth)
        }
      }
      window.addEventListener('pointermove', onMove)
      window.addEventListener('pointerup', onUp)
    },
    [rightPaneWidth, rightPane],
  )

  useLayoutEffect(() => {
    // Measure the grid scroller's vertical scrollbar width so the header spacer
    // keeps header/body columns aligned.
    const el = bodyViewportRef.current
    if (!el) return
    // offsetWidth - clientWidth = the vertical scrollbar's width (0 for
    // overlay scrollbars or when rows don't overflow). The ResizeObserver
    // re-measures when the scrollbar appears/disappears — that changes the
    // element's content box.
    const measure = () => setScrollbarWidth(el.offsetWidth - el.clientWidth)
    measure()
    if (typeof ResizeObserver === 'undefined') return
    const observer = new ResizeObserver(measure)
    observer.observe(el)
    return () => observer.disconnect()
  }, [])

  // ─── State ───────────────────────────────────────────────
  const gridState = useGridState({ initialSorting })
  useGridColumnStatePersistence(gridState, persistStateKey)
  const {
    searchValue,
    onSearchChange,
    onClearFilters,
    hasActiveFilters,
    setColumnSizing,
    userColumnVisibility,
    setUserColumnVisibility,
    columnOrder,
    setColumnOrder,
    setColumnPinning,
    resetColumnState,
  } = gridState
  const resizeGuard = useResizeClickGuard()
  /** Enter or Space on a filter trigger opens its popover — the popover
   *  itself only listens for clicks. */
  const openOnKey = (columnId: string) => (e: React.KeyboardEvent) => {
    if (e.key !== 'Enter' && e.key !== ' ') return
    e.preventDefault()
    e.stopPropagation()
    setOpenFilterColumnId(columnId)
  }
  const [openFilterColumnId, setOpenFilterColumnId] = useState<string | null>(
    null,
  )
  // One open column menu at a time, owned here (same anchor-stability pattern
  // as openFilterColumnId — a controlled dropdown must keep its trigger DOM).
  const [openMenuColumnId, setOpenMenuColumnId] = useState<string | null>(null)
  // Choose Columns modal — one per grid, opened from any column's menu.
  const [isColumnChooserOpen, setIsColumnChooserOpen] = useState(false)
  // Expanded date-tree node keys per column, owned here (above the filter
  // popover). The popover's content is rebuilt whenever the column filter
  // changes, so state kept inside the DateFilterPanel is lost on toggle; keeping
  // it on the grid makes the tree's expand/collapse survive checkbox changes.
  const [dateTreeExpanded, setDateTreeExpanded] = useState<
    Record<string, Set<string>>
  >({})

  // ─── Tree-mode state (drafts + DnD) ──────────────────────
  const [draggedNodeId, setDraggedNodeId] = useState<string | null>(null)
  const [draftTasks, setDraftTasks] = useState<DraftItem[]>([])
  // Read by the drag handlers, which are created before the table exists; a
  // layout effect publishes it ahead of any passive effect.
  const tableRef = useRef<Table<T> | null>(null)

  // ─── Draft management (tree mode) ────────────────────────
  const dataWithDrafts = useMemo(() => {
    if (!createDraftNode || draftTasks.length === 0) return data
    // Draft rows require tree-shaped nodes ({ id, children }); the cast is
    // localized here (same contract the old TreeGrid enforced via its bound).
    return mergeDraftsIntoTree(
      data as unknown as TreeNode[],
      draftTasks,
      createDraftNode as unknown as (draft: DraftItem) => TreeNode,
    ) as unknown as T[]
  }, [data, draftTasks, createDraftNode])

  // ─── Cell editing ────────────────────────────────────────
  const cellEditingEnabled = !!onCommit
  const messageApi = useOptionalMessage()
  const cellEditing = useGridCellEditing<T>({
    enabled: cellEditingEnabled,
    data: dataWithDrafts,
    serverData: data,
    isTree,
    rowKeyOf: (row) => {
      if (getRowId) return getRowId(row)
      const id = (row as { id?: string | number }).id
      return id != null ? String(id) : undefined
    },
    canCreateDrafts: !!createDraftNode && !!onCreate && !isLoading,
    draftCount: draftTasks.length,
    insertDraft: (draft) => setDraftTasks((prev) => [...prev, draft]),
    onCommit,
    onCreate,
    fieldToColumn,
    draftParentId: (key) =>
      draftTasks.find((draft) => draft.id === key)?.parentId ?? null,
    draftOrder: (key) => {
      const afterId = draftTasks.find((draft) => draft.id === key)?.afterId
      return afterId
        ? orderAfter(data as unknown as TreeNode[], afterId)
        : undefined
    },
    removeDraft: (key, created) => {
      setDraftTasks((prev) => prev.filter((draft) => draft.id !== key))
      if (!created) onDraftCancelled?.(key)
    },
    notify: messageApi ? (message) => messageApi.error(message) : undefined,
    bodyViewportRef,
  })
  const { attachTable } = cellEditing

  const isEditing = !!cellEditing.open

  // ─── Column context ──────────────────────────────────────
  // Tree drags reparent (onNodeMove); flat drags reorder (onRowReorder).
  const dragEnabledBase =
    (isTree
      ? enableDragAndDrop && !!onNodeMove
      : !isGrouped && !!onRowReorder) &&
    !isLoading &&
    !isEditing

  const { canCreateDraft, addDraft } = cellEditing

  const columnContext: GridColumnContext = useMemo(() => {
    const dragEnabledForColumns = dragEnabledBase && !hasActiveFilters

    return {
      isDragEnabled: dragEnabledForColumns,
      canCreateDraft,
      addDraftAtRoot: () => addDraft(),
      addDraftAsChild: (parentId: string) => addDraft(parentId),
    }
  }, [hasActiveFilters, dragEnabledBase, canCreateDraft, addDraft])

  // ─── Resolved columns ───────────────────────────────────
  // Resolved outside the memo below and passed as a boolean: keying the column
  // defs on `data` would rebuild them on every refetch, which is the header
  // churn the note on resolvedColumns describes. rowsHaveId reads one row.
  const injectIdColumn = includeIdColumn && rowsHaveId(data)

  const columns = useMemo(() => {
    const resolved =
      typeof columnsProp === 'function'
        ? columnsProp(columnContext)
        : columnsProp
    return withIdColumn(resolved, injectIdColumn) as typeof resolved
  }, [columnsProp, columnContext, injectIdColumn])

  // applySafeAccessor must run before applyColumnType (the type's raw-value
  // reader handles accessorFns but not dotted keys). Memoized on `columns`:
  // rebuilding defs every render made TanStack rebuild its headers, replacing
  // the filter Popover's trigger DOM node — which antd read as a click-outside
  // and closed the open popover on every checkbox toggle.
  const resolvedColumns = useMemo(
    () =>
      columns.map((col) =>
        applyColumnType(applySafeAccessor(col as ColumnDef<T, unknown>)),
      ) as typeof columns,
    [columns],
  )

  // meta.unavailable → columnVisibility, recursing into grouped defs (TanStack
  // shrinks a band's colSpan as its leaves hide).
  const consumerColumnVisibility = useMemo<VisibilityState>(() => {
    const visibility: VisibilityState = {}
    const collect = (cols: typeof resolvedColumns) => {
      for (const col of cols) {
        const children = (col as { columns?: typeof resolvedColumns }).columns
        if (children) collect(children)
        const meta = col.meta
        if (meta?.unavailable === undefined) continue
        const id =
          col.id ??
          (col as { accessorKey?: string | number }).accessorKey?.toString()
        if (id) visibility[id] = !meta.unavailable
      }
    }
    collect(resolvedColumns)
    return visibility
  }, [resolvedColumns])

  // meta.hiddenByDefault → the layer *below* the user's choices, so the column
  // starts hidden, stays in the chooser, and returns to hidden on reset.
  const defaultColumnVisibility = useMemo<VisibilityState>(() => {
    const visibility: VisibilityState = {}
    const collect = (cols: typeof resolvedColumns) => {
      for (const col of cols) {
        const children = (col as { columns?: typeof resolvedColumns }).columns
        if (children) collect(children)
        if (col.meta?.hiddenByDefault !== true) continue
        const id =
          col.id ??
          (col as { accessorKey?: string | number }).accessorKey?.toString()
        if (id) visibility[id] = false
      }
    }
    collect(resolvedColumns)
    return visibility
  }, [resolvedColumns])

  // The user's Choose Columns choices layer on top: consumer-hidden columns
  // stay hidden (and out of the chooser); user choices win everywhere else.
  const columnVisibility = useMemo<VisibilityState>(
    () =>
      mergeColumnVisibility(
        consumerColumnVisibility,
        userColumnVisibility,
        defaultColumnVisibility,
      ),
    [consumerColumnVisibility, userColumnVisibility, defaultColumnVisibility],
  )

  // Leaf column ids in DEF order (recursing bands), for reconciling the
  // persisted/user column order against the live defs.
  const leafColumnIds = useMemo<string[]>(() => {
    const ids: string[] = []
    const collect = (cols: typeof resolvedColumns) => {
      for (const col of cols) {
        const children = (col as { columns?: typeof resolvedColumns }).columns
        if (children) {
          collect(children)
          continue
        }
        const id =
          col.id ??
          (col as { accessorKey?: string | number }).accessorKey?.toString()
        if (id) ids.push(id)
      }
    }
    collect(resolvedColumns)
    return ids
  }, [resolvedColumns])

  // The order actually fed to TanStack: the user's order reconciled against the
  // live defs so removed columns drop out and columns added since (or absent
  // from a persisted order) land at their def position instead of being
  // appended to the end. The RAW gridState.columnOrder is what persists — this
  // derivative never round-trips to storage, so defs changing between pages
  // can't rewrite the stored order.
  const effectiveColumnOrder = useMemo(
    () => reconcileColumnOrder(columnOrder, leafColumnIds),
    [columnOrder, leafColumnIds],
  )

  // Column reordering is on for every flat/tree grid EXCEPT grouped-header
  // grids: reordering leaves across band boundaries would split bands, so the
  // simplest safe scope is to disable header-drag reordering when any def is a
  // band. (The modal reorder is likewise gated on this.)
  const hasGroupedHeaders = useMemo(
    () =>
      resolvedColumns.some(
        (col) => (col as { columns?: unknown[] }).columns !== undefined,
      ),
    [resolvedColumns],
  )
  const columnReorderEnabled = !hasGroupedHeaders

  // ─── Flattened tree for DnD ──────────────────────────────
  const flattenedNodes = useMemo(
    () => (isTree ? flattenTree(dataWithDrafts as unknown as TreeNode[]) : []),
    [isTree, dataWithDrafts],
  )

  // ─── DnD setup ───────────────────────────────────────────
  const sensors = useGridDndSensors()

  const handleDragStart = useCallback((event: DragStartEvent) => {
    setDraggedNodeId(event.active.id as string)
  }, [])

  const handleDragEnd = useCallback(
    async (event: DragEndEvent) => {
      const { active, over, delta } = event
      setDraggedNodeId(null)

      if (!over || !onNodeMove) return

      const horizontalOffset = delta.x
      const hasHorizontalMovement =
        Math.abs(horizontalOffset) >= INDENTATION_WIDTH / 2

      if (active.id === over.id && !hasHorizontalMovement) return

      const projection = getProjection(
        flattenedNodes,
        active.id as string,
        over.id as string,
        horizontalOffset,
        INDENTATION_WIDTH,
        moveValidator,
      )

      if (!projection.canDrop) {
        onMoveRejected?.(
          projection.reason || 'Cannot move item to this location',
        )
        return
      }

      const overIndex = flattenedNodes.findIndex((t) => t.node.id === over.id)
      const newOrder = calculateOrderInParent(
        flattenedNodes,
        active.id as string,
        overIndex,
        projection.parentId,
      )

      try {
        await onNodeMove(
          active.id as string,
          projection.parentId,
          newOrder,
          over.id as string,
          overIndex,
        )
      } catch (error) {
        onMoveRejected?.(
          error instanceof Error ? error.message : 'Failed to move item',
        )
      }
    },
    [flattenedNodes, moveValidator, onNodeMove, onMoveRejected],
  )

  const handleDragCancel = useCallback((_event: DragCancelEvent) => {
    setDraggedNodeId(null)
    if (document.activeElement instanceof HTMLElement) {
      document.activeElement.blur()
    }
  }, [])

  // ─── Flat row-reorder DnD ────────────────────────────────
  /** Stable sortable id for a flat row: getRowId → row.original.id → row.id. */
  const flatRowId = useCallback(
    (row: Row<T>): string => {
      if (getRowId) return getRowId(row.original)
      const id = (row.original as { id?: string | number }).id
      return id != null ? String(id) : row.id
    },
    [getRowId],
  )

  const handleFlatDragEnd = useCallback(
    (event: DragEndEvent) => {
      const { active, over } = event
      setDraggedNodeId(null)

      if (!over || !onRowReorder || active.id === over.id) return

      const displayed = (tableRef.current?.getRowModel().rows ?? []) as Row<T>[]
      const fromIndex = displayed.findIndex((r) => flatRowId(r) === active.id)
      const toIndex = displayed.findIndex((r) => flatRowId(r) === over.id)
      if (fromIndex < 0 || toIndex < 0 || fromIndex === toIndex) return

      // Fire-and-forget: reorder consumers own their error handling (they
      // revert by refetching), same as the tree onNodeMove contract.
      void onRowReorder({
        orderedData: arrayMove(
          displayed.map((r) => r.original),
          fromIndex,
          toIndex,
        ),
        activeId: String(active.id),
        fromIndex,
        toIndex,
      })
    },
    [flatRowId, onRowReorder],
  )

  // ─── TanStack table ─────────────────────────────────────
  const table = useGridTable({
    data: cellEditing.data,
    columns: resolvedColumns,
    gridState,
    tableOptions: {
      defaultColumn: {
        filterFn: waydColumnFilter,
        sortFn: sortEmptyLast,
      },
      ...(getRowId ? { getRowId: (row: T) => getRowId(row) } : {}),
      // Tree mode: rows expand, and a filter match keeps the ancestor chain
      // visible (filterFromLeafRows).
      ...(isTree
        ? {
            getSubRows,
            filterFromLeafRows: true,
            initialState: { expanded: initialExpanded ? (true as const) : {} },
          }
        : {}),
      // Grouped mode: the grouped column leaves the row cells (its value is the
      // heading), and groups start expanded. TanStack resets `expanded` to this
      // initial state whenever the grouping changes, so a regrouped grid opens
      // fully rather than inheriting the previous grouping's collapsed ids.
      ...(isGrouped
        ? {
            groupedColumnMode: 'remove' as const,
            initialState: {
              expanded: initialGroupsExpanded ? (true as const) : {},
            },
          }
        : {}),
      ...(onRowSelectionChange ? { onRowSelectionChange } : {}),
    },
    extraState: {
      columnVisibility,
      columnOrder: effectiveColumnOrder,
      ...(rowSelection ? { rowSelection } : {}),
      // The grouped columns lead the sort so the groups themselves come out in
      // the grouped column's order; the user's sort then applies within each
      // group. Without this a group row sorts on aggregates it does not have
      // and the groups keep whatever order the data arrived in.
      ...(isGrouped
        ? {
            grouping,
            sorting: [
              ...grouping.map((id) => ({ id, desc: false })),
              ...gridState.sorting.filter((s) => !grouping.includes(s.id)),
            ],
          }
        : {}),
    },
  })

  useLayoutEffect(() => {
    tableRef.current = table
    attachTable(table)
  }, [table, attachTable])

  useImperativeHandle(ref, () => ({
    table,
    getDisplayedRows: () =>
      table
        .getRowModel()
        .rows.filter((row: Row<T>) => !row.getIsGrouped())
        .map((row: Row<T>) => row.original),
    commitCells: cellEditing.commitCells,
    focusRow: (rowId: string, columnId: string) =>
      cellEditing.requestFocus(rowId, columnId),
  }))

  // ─── Row-action keys ─────────────────────────────────────
  const isDraftKey = (key: string) => key.startsWith(DRAFT_PREFIX)
  // Opt-in through rowActions: a grid that takes these keys must place a new
  // row where Insert puts it (onCreate's order) and reject a failed move.
  const canInsertRows =
    !!rowActions &&
    isTree &&
    cellEditingEnabled &&
    !!createDraftNode &&
    !!onCreate
  // Needs cell editing for its live region and for focus to follow the row.
  const canMoveRows =
    !!rowActions &&
    isTree &&
    cellEditingEnabled &&
    enableDragAndDrop &&
    !!onNodeMove &&
    !isLoading
  const rowActionSupport = {
    insert: canInsertRows,
    move: canMoveRows,
    delete: !!rowActions?.onDelete,
    open: !!rowActions?.onOpen,
  }
  const hasRowActions = Object.values(rowActionSupport).some(Boolean)

  const [footerHintsHost, setFooterHintsHost] = useState<HTMLDivElement | null>(
    null,
  )

  // A second move sent before the first lands would be worked out from the
  // tree as it was, and land somewhere the user did not ask for.
  const movingRef = useRef(false)

  const runRowAction = (
    action: RowActionKey,
    row: Row<T>,
  ): boolean | Promise<false | { parentId: string | null }> => {
    const key = cellEditing.rowKeyOf(row.original)
    if (!key || isDraftKey(key)) return false
    // A key the grid takes but cannot act on says why, so a keyboard user
    // can tell a refusal from a key that did nothing.
    const refuse = (message: string) => {
      cellEditing.announce(message)
      return true
    }
    switch (action) {
      case 'insert': {
        if (!canInsertRows) return false
        // The new row's place is taken from the data order, which a sort
        // or filter no longer shows.
        if (hasActiveFilters) {
          return refuse('Clear the search, filters and sorting to add a row.')
        }
        const parent = row.getParentRow()?.original ?? null
        if (rowActions?.canInsertUnder && !rowActions.canInsertUnder(parent)) {
          return refuse('A row cannot be added here.')
        }
        const parentKey = parent ? cellEditing.rowKeyOf(parent) : undefined
        if (!cellEditing.addDraft(parentKey, key)) {
          return refuse('Finish the new row first.')
        }
        return true
      }
      case 'indent':
      case 'outdent': {
        if (!canMoveRows) return false
        // Refused under a filter or sort, as dragging is: a move works in data
        // order and could nest the row under one the user cannot see.
        if (hasActiveFilters) {
          return refuse('Clear the search, filters and sorting to move a row.')
        }
        const nodes = flattenedNodes
        const move = (action === 'indent' ? indentMove : outdentMove)(
          nodes,
          key,
          isDraftKey,
        )
        const active = nodes.find((entry) => entry.node.id === key)
        if (!move || !active) {
          return refuse(
            action === 'indent'
              ? 'There is no row above at this level to indent under.'
              : 'The row is already at the top level.',
          )
        }
        const verdict = (moveValidator ?? defaultMoveValidator)(
          active,
          move.parent,
          move.parentId,
        )
        if (!verdict.canMove) {
          const reason = verdict.reason || 'Cannot move item to this location'
          // A consumer's onMoveRejected shows it already, as an alert.
          if (onMoveRejected) {
            onMoveRejected(reason)
            return true
          }
          return refuse(reason)
        }
        if (movingRef.current) {
          return refuse('Wait for the last move to finish.')
        }
        movingRef.current = true
        const moving = onNodeMove!(key, move.parentId, move.order)
        void moving.then(
          () => (movingRef.current = false),
          () => (movingRef.current = false),
        )
        return moving.then(
          () => {
            // A row indented under a collapsed one would vanish, and focus
            // could not follow it.
            const parent = table
              .getRowModel()
              .rows.find(
                (r: Row<T>) =>
                  cellEditing.rowKeyOf(r.original) === move.parentId,
              )
            if (parent && !parent.getIsExpanded()) parent.toggleExpanded(true)
            cellEditing.announce(
              action === 'indent' ? 'Indented.' : 'Outdented.',
            )
            return { parentId: move.parentId }
          },
          (error: unknown) => {
            const reason =
              error instanceof Error ? error.message : 'Failed to move item'
            if (onMoveRejected) onMoveRejected(reason)
            else cellEditing.announce(reason)
            return false
          },
        )
      }
      case 'delete':
        if (!rowActions?.onDelete) return false
        return (
          rowActions.onDelete(row.original) !== false ||
          refuse('This row cannot be deleted.')
        )
      case 'open':
        if (!rowActions?.onOpen) return false
        return (
          rowActions.onOpen(row.original) !== false ||
          refuse('This row has no details to open.')
        )
    }
  }

  // ─── Header column-reorder DnD ───────────────────────────
  // Section-aware move shared by header-grip drag and the Choose Columns modal.
  // Reorders WITHIN the active column's section only: the center section via
  // columnOrder, each pinned side via its columnPinning array (TanStack orders
  // pinned sections by the pin array, ignoring columnOrder). A cross-section
  // move, a no-op, or a move touching a non-reorderable column is ignored —
  // dragging never pins/unpins (that stays the column menu's job).
  const moveColumn = useCallback(
    (activeId: string, overId: string) => {
      if (activeId === overId) return

      const activeColumn = table.getColumn(activeId)
      const overColumn = table.getColumn(overId)
      if (
        activeColumn?.columnDef.meta?.enableReordering === false ||
        overColumn?.columnDef.meta?.enableReordering === false
      ) {
        return
      }

      const activeSide = activeColumn?.getIsPinned() ?? false
      const overSide = overColumn?.getIsPinned() ?? false
      if (activeSide !== overSide) return

      if (activeSide === false) {
        // Center: reorder within the full columnOrder (pinned ids ride along
        // inertly). Seed from the reconciled order so a first-ever reorder
        // persists a complete, self-consistent order.
        setColumnOrder(reorderIds(effectiveColumnOrder, activeId, overId))
        return
      }

      // Pinned: reorder that side's pin array.
      setColumnPinning((prev) => {
        const side = prev[activeSide] ?? []
        const next = reorderIds(side, activeId, overId)
        if (next === side) return prev
        return { ...prev, [activeSide]: next }
      })
    },
    [table, effectiveColumnOrder, setColumnOrder, setColumnPinning],
  )

  const handleColumnDragEnd = useCallback(
    (event: DragEndEvent) => {
      const { active, over } = event
      if (!over) return
      // Sortable ids are namespaced `col:<id>` so a shared context could tell
      // column drags from row drags; strip it back to the column id.
      moveColumn(
        String(active.id).replace(/^col:/, ''),
        String(over.id).replace(/^col:/, ''),
      )
    },
    [moveColumn],
  )

  const rows = table.getRowModel().rows

  // Displayed-rows callback: TanStack memoizes the row model, so `rows` only
  // changes identity when the data / filters / sorting actually change — the
  // effect fires exactly on displayed-set changes (plus once on mount).
  // Group headings are not records: the displayed-rows surfaces report the
  // data rows only, whatever the grid put between them.
  const dataRows = isGrouped ? rows.filter((row) => !row.getIsGrouped()) : rows
  useEffect(() => {
    onDisplayedRowsChange?.(dataRows.map((row) => row.original))
  }, [dataRows, onDisplayedRowsChange])
  const displayedRowCount = dataRows.length
  const totalRowCount = isTree
    ? countTreeNodes(data as unknown as TreeNode[]) + draftTasks.length
    : data.length
  const visibleColumnCount = table.getVisibleLeafColumns().length

  // Columns whose BODY cells right-align: explicit meta.align wins, otherwise
  // numeric columns (same resolution as the filter UI). Headers stay
  // left-aligned regardless. Recomputed when the data changes — the inference
  // samples the faceted values, which are empty until rows arrive.
  const numericColumnIds = useMemo(() => {
    // The table is a stable identity whose faceted samples mutate underneath;
    // data and resolvedColumns are the real recompute triggers.
    void data
    void resolvedColumns
    const ids = new Set<string>()
    for (const column of table.getAllLeafColumns()) {
      const align = column.columnDef.meta?.align
      if (align === 'left') continue
      if (align === 'right' || resolveColumnFilterType(column) === 'number') {
        ids.add(column.id)
      }
    }
    return ids
  }, [table, data, resolvedColumns])

  const toggleDateTreeNode = useCallback(
    (columnId: string, nodeKey: string) => {
      setDateTreeExpanded((prev) => {
        const current = prev[columnId] ?? EMPTY_NODE_SET
        const next = new Set(current)
        if (next.has(nodeKey)) next.delete(nodeKey)
        else next.add(nodeKey)
        return { ...prev, [columnId]: next }
      })
    },
    [],
  )

  // ─── Column menu (sort / pin / autosize / choose / reset) ──
  /**
   * Sizes columns to their rendered content (header label + the virtualized
   * window's cells — all the DOM that exists; ag-grid measured the same way).
   * Applies through columnSizing so the shared colgroup updates both tables.
   */
  const autosizeColumns = useCallback(
    (columnIds: string[]) => {
      const headerRoot = headerViewportRef.current
      const bodyRoot = bodyViewportRef.current
      if (!headerRoot || !bodyRoot) return
      const measured = measureColumnContent(headerRoot, bodyRoot, columnIds)
      setColumnSizing((prev) => {
        const next = { ...prev }
        for (const [id, m] of measured) {
          const columnDef = table.getColumn(id)?.columnDef
          next[id] = computeAutosizeWidth({
            maxCellContentWidth: m.maxCellContentWidth,
            headerContentWidth: m.headerContentWidth,
            minWidth: columnDef?.minSize,
            maxWidth: columnDef?.maxSize,
          })
        }
        return next
      })
    },
    [setColumnSizing, table],
  )

  const autosizeAllColumns = useCallback(() => {
    autosizeColumns(
      getOrderedVisibleLeafColumns(table)
        .filter((column) => column.getCanResize())
        .map((column) => column.id),
    )
  }, [autosizeColumns, table])

  const handleUserToggleColumn = useCallback(
    (columnId: string, visible: boolean) => {
      setUserColumnVisibility((prev) => ({ ...prev, [columnId]: visible }))
    },
    [setUserColumnVisibility],
  )

  // Computed once per grid render and shared by every header's menu trigger
  // (each trigger deriving it would rescan all leaf columns per column).
  const hasHidableColumns = getColumnChooserOptions(table).length > 0

  const renderColumnMenu = (header: Header<T, unknown>) => (
    <ColumnMenuTrigger
      header={header}
      table={table}
      open={openMenuColumnId === header.column.id}
      onOpenChange={(open) =>
        setOpenMenuColumnId(open ? header.column.id : null)
      }
      hasHidableColumns={hasHidableColumns}
      onOpenColumnChooser={() => setIsColumnChooserOpen(true)}
      onAutosizeColumn={(columnId) => autosizeColumns([columnId])}
      onAutosizeAllColumns={autosizeAllColumns}
      onResetColumns={resetColumnState}
    />
  )

  // ─── CSV export ──────────────────────────────────────────
  const onExportCsv = useCallback(() => {
    exportGridToCsv(table, csvFileName)
  }, [table, csvFileName])

  const showColumnFilters = useMemo(
    () =>
      includeColumnFilters &&
      table.getVisibleLeafColumns().some((c) => c.getCanFilter()),
    [includeColumnFilters, table],
  )

  const showFloatingFilters = showColumnFilters && includeFloatingFilters

  const columnFilterType = (header: Header<T, unknown>): FilterType =>
    resolveColumnFilterType(header.column)

  /**
   * Wraps a trigger element in the multi-condition {@link FilterPopup} popover.
   * `trigger` defaults to the filter-icon button (used in the header and to the
   * right of floating inputs); callers can pass their own trigger — e.g. the
   * set-filter floating summary, so clicking it opens the same popup.
   */
  const renderFilterPopover = (
    header: Header<T, unknown>,
    trigger?: ReactNode,
  ) => {
    const meta = header.column.columnDef.meta
    const filterType = columnFilterType(header)
    const filterValue = header.column.getFilterValue() as
      ColumnFilterModel | undefined
    const isFiltered = filterValue !== undefined
    const isFilterOpen = openFilterColumnId === header.column.id
    // Text columns can opt into a combined text + set panel.
    const combined = filterType === 'text' && meta?.filterEnableSet === true
    // Date columns get the Excel-style panel (date tree + relative + conditions).
    const isDate = filterType === 'date'

    return (
      <Popover
        trigger="click"
        placement="bottomRight"
        open={isFilterOpen}
        onOpenChange={(open) =>
          setOpenFilterColumnId(open ? header.column.id : null)
        }
        getPopupContainer={() => document.body}
        // See renderSetFilter: the panels focus on mount, so the popup must not
        // stay alive between opens.
        destroyOnHidden
        content={
          // Stop clicks inside the popup from bubbling to the <th> sort handler
          // (React events propagate through the portal to the logical parent).
          <div onClick={(e) => e.stopPropagation()}>
            {isDate ? (
              <DateFilterPanel
                dayKeys={getDayKeys(header)}
                value={filterValue}
                maxConditions={meta?.maxFilterConditions}
                onChange={(next) => header.column.setFilterValue(next)}
                expandedNodes={
                  dateTreeExpanded[header.column.id] ?? EMPTY_NODE_SET
                }
                onToggleNode={(key) =>
                  toggleDateTreeNode(header.column.id, key)
                }
              />
            ) : combined ? (
              <CombinedFilterPanel
                allValues={getSetValues(header)}
                labels={meta?.filterOptions}
                value={filterValue}
                maxConditions={meta?.maxFilterConditions}
                onChange={(next) => header.column.setFilterValue(next)}
                onCommit={() => setOpenFilterColumnId(null)}
              />
            ) : (
              <FilterPopup
                filterType={filterType}
                value={filterValue}
                maxConditions={meta?.maxFilterConditions}
                onChange={(next) => header.column.setFilterValue(next)}
              />
            )}
          </div>
        }
      >
        {trigger ?? (
          <span
            role="button"
            tabIndex={0}
            onKeyDown={openOnKey(header.column.id)}
            aria-label={
              isFiltered ? 'Edit column filter (active)' : 'Filter column'
            }
            className={`${styles.filterTrigger}${
              isFiltered ? ` ${styles.filterTriggerActive}` : ''
            }`}
            onClick={(e) => e.stopPropagation()}
            // Header cell is a reorder drag handle — don't drag from the icon.
            onPointerDown={(e) => e.stopPropagation()}
          >
            {isFiltered ? <FilterFilled /> : <FilterOutlined />}
          </span>
        )}
      </Popover>
    )
  }

  /**
   * All known set values for a column: prefer declared options (stable
   * order/labels), else the distinct values present in the data (faceted).
   * For a multi-value column (`meta.multiValues`), the values are read from
   * each faceted row, so the list shows individual values rather than whole
   * combinations, and a value containing a comma stays whole. When the data
   * contains blank
   * cells (null/undefined/''), a "(Blanks)" sentinel entry is appended so
   * blanks can be filtered like any value.
   */
  const getSetValues = (header: Header<T, unknown>): string[] => {
    const meta = header.column.columnDef.meta
    const facetedKeys = Array.from(
      header.column.getFacetedUniqueValues().keys(),
    )
    const getRowValues = meta?.multiValues
    // flatRows, as TanStack's faceted unique values read: in a tree grid `rows`
    // holds only the roots, which would drop every nested row's values.
    const rowValues = getRowValues
      ? header.column
          .getFacetedRowModel()
          .flatRows.map((row) => getRowValues(row.original))
      : undefined
    const hasBlanks = rowValues
      ? rowValues.some((values) => values.length === 0)
      : facetedKeys.some((v) => v == null || v === '')
    const optionValues = meta?.filterOptions?.map((o) => o.value)
    const distinct = rowValues
      ? Array.from(new Set(rowValues.flat()))
      : facetedKeys
          .filter((v): v is string => v != null && v !== '')
          .map(String)
    const values = optionValues ?? distinct.sort(caseInsensitiveCompare)
    return hasBlanks ? [...values, SET_FILTER_BLANK] : values
  }

  /**
   * Distinct `YYYY-MM-DD` day keys present in a date column, for the date tree.
   * Each faceted value is normalized to its day (dropping time-of-day and raw
   * shape differences) and de-duplicated.
   */
  const getDayKeys = (header: Header<T, unknown>): string[] => {
    const keys = new Set<string>()
    for (const v of header.column.getFacetedUniqueValues().keys()) {
      const key = toDayKey(v)
      if (key) keys.add(key)
    }
    return Array.from(keys).sort()
  }

  /**
   * Excel/AG Grid-style set filter for a column: a compact display box plus a
   * filter icon that opens the {@link SetFilterPanel} (search + Select All +
   * checkboxes). Values are derived from the data (faceted unique values),
   * falling back to the column's declared filterOptions.
   */
  const renderSetFilter = (header: Header<T, unknown>) => {
    const column = header.column
    const meta = column.columnDef.meta
    const filterValue = column.getFilterValue() as ColumnFilterModel | undefined
    const isFiltered = filterValue !== undefined
    const isFilterOpen = openFilterColumnId === column.id

    const allValues = getSetValues(header)

    const selected =
      filterValue?.type === 'set' ? filterValue.values : allValues
    const summary =
      !isFiltered || selected.length === allValues.length
        ? ''
        : selected.length === 1
          ? selected[0] === SET_FILTER_BLANK
            ? SET_FILTER_BLANK_LABEL
            : (meta?.filterOptions?.find((o) => o.value === selected[0])
                ?.label ?? selected[0])
          : `${selected.length} selected`

    return (
      <Popover
        trigger="click"
        placement="bottomLeft"
        open={isFilterOpen}
        onOpenChange={(open) => setOpenFilterColumnId(open ? column.id : null)}
        getPopupContainer={() => document.body}
        // Remount the panel per open: it focuses its search box on mount, and a
        // kept-alive popup would only ever focus the first time.
        destroyOnHidden
        content={
          <div onClick={(e) => e.stopPropagation()}>
            <SetFilterPanel
              allValues={allValues}
              labels={meta?.filterOptions}
              value={filterValue}
              onChange={(next) => column.setFilterValue(next)}
              onCommit={() => setOpenFilterColumnId(null)}
            />
          </div>
        }
      >
        <div
          role="button"
          tabIndex={0}
          onKeyDown={openOnKey(column.id)}
          aria-label={isFiltered ? 'Filter column (active)' : 'Filter column'}
          className={styles.floatingCell}
        >
          <span className={styles.setSummary}>{summary}</span>
          <span
            className={`${styles.filterTrigger}${
              isFiltered ? ` ${styles.filterTriggerActive}` : ''
            }`}
          >
            {isFiltered ? <FilterFilled /> : <FilterOutlined />}
          </span>
        </div>
      </Popover>
    )
  }

  // ─── Resolved leftSlot ──────────────────────────────────
  const resolvedLeftSlot =
    typeof leftSlot === 'function' ? leftSlot(columnContext) : leftSlot

  // ─── DnD wrapping ───────────────────────────────────────
  const treeDndEnabled = isTree && enableDragAndDrop && !!onNodeMove
  // Grouped rows are displayed in group order, not data order, so there is
  // nothing a drag could reorder.
  const flatDndEnabled = !isTree && !isGrouped && !!onRowReorder
  const dndEnabled = treeDndEnabled || flatDndEnabled
  const isDragEnabled =
    dndEnabled && !isLoading && !isEditing && !hasActiveFilters

  // ─── Render ──────────────────────────────────────────────
  // TanStack returns one header group per depth; the LAST is always the leaf
  // columns, so any rows above it render as colspan bands. The band count
  // drives the sticky-offset CSS var (bands / leaf header / filter row stack).
  const headerGroups = table.getHeaderGroups()
  const leafHeaderGroup = headerGroups[headerGroups.length - 1]
  const groupHeaderRows = headerGroups.slice(0, -1)

  // Visible leaf columns in RENDER order: left-pinned → center → right-pinned
  // (center honours columnOrder). getHeaderGroups() and row.getVisibleCells()
  // emit this order, but plain getVisibleLeafColumns() stays in def order — the
  // colgroup must follow the rendered order or the shared <col> widths would
  // apply to the wrong columns.
  const orderedVisibleLeafColumns = getOrderedVisibleLeafColumns(table)

  // Sortable ids for the header row's SortableContext, namespaced `col:` (the
  // same ids the header cells' useSortable use). In display order so dnd-kit's
  // index math lines up with what the user sees.
  const columnSortableIds = orderedVisibleLeafColumns.map(
    (column) => `col:${column.id}`,
  )

  // Rendered into BOTH tables (header + body) — with table-layout: fixed,
  // identical colgroups guarantee the two tables' columns align exactly.
  const colGroup = (
    <colgroup>
      {orderedVisibleLeafColumns.map((column) => (
        <col key={column.id} width={column.getSize()} />
      ))}
      {/* Filler column absorbs leftover width so the table fills the
          viewport (header band + borders edge-to-edge) without widening
          the real columns. */}
      <col className={styles.fillerCol} />
    </colgroup>
  )

  // Header rows above the body, for each row's aria-rowindex.
  const headerRowCount =
    groupHeaderRows.length + 1 + (showFloatingFilters ? 1 : 0)

  // The header table, optionally wrapped in a column-reorder DnD context. The
  // context is header-scoped and separate from the row/tree DnD context below,
  // so column drags and row drags never share sensors or collision detection.
  const headerTable = (
    <table
      className={styles.tableElement}
      role="presentation"
      style={
        {
          '--wayd-grid-group-header-rows': groupHeaderRows.length,
        } as React.CSSProperties
      }
    >
      {colGroup}
      <thead role="rowgroup">
        {/* Grouped-header band rows — plain colspan cells, no
              sort/filter/resize; placeholders render empty. */}
        {groupHeaderRows.map((headerGroup, bandIndex) => (
          <tr
            key={headerGroup.id}
            data-role="header-band"
            role="row"
            aria-rowindex={bandIndex + 1}
          >
            {headerGroup.headers.map((header, headerIndex) => {
              // TanStack splits a band spanning pinned + unpinned leaves
              // into one cell per pin section; a pinned section's cell
              // sticks at its leaves' offset.
              const bandPinned = getPinnedBandOffsets(header)
              // A band's own index counts bands, not columns.
              const firstColumn = headerGroup.headers
                .slice(0, headerIndex)
                .reduce((sum, previous) => sum + previous.colSpan, 1)
              return (
                <th
                  key={header.id}
                  colSpan={header.colSpan}
                  role="columnheader"
                  aria-colindex={firstColumn}
                  aria-colspan={header.colSpan}
                  // Placeholders (ungrouped columns passing through the band
                  // level) are empty — hide them from assistive tech so
                  // screen readers don't announce blank column headers.
                  aria-hidden={header.isPlaceholder || undefined}
                  className={`${styles.th} ${styles.groupTh}${
                    bandPinned
                      ? ` ${pinnedCellClassNames(bandPinned, headerPinnedClasses)}`
                      : ''
                  }`}
                  style={{
                    top: `calc(${bandIndex} * var(--wayd-grid-header-row-height))`,
                    ...pinnedCellStyle(bandPinned),
                  }}
                >
                  <GridHeaderContent header={header} />
                </th>
              )
            })}
            {/* Filler band cell — carries the band across the empty width. */}
            <th
              aria-hidden="true"
              className={`${styles.th} ${styles.groupTh} ${styles.fillerTh}`}
              style={{
                top: `calc(${bandIndex} * var(--wayd-grid-header-row-height))`,
              }}
            />
          </tr>
        ))}

        {/* Leaf header row */}
        <tr
          key={leafHeaderGroup.id}
          role="row"
          aria-rowindex={groupHeaderRows.length + 1}
        >
          {leafHeaderGroup.headers.map((header) => {
            // When floating filters are shown, the popup trigger lives
            // in the floating row instead of the header.
            const showHeaderFilterIcon =
              showColumnFilters &&
              !showFloatingFilters &&
              !header.isPlaceholder &&
              header.column.getCanFilter()
            const pinned = getPinnedOffsets(header.column)
            const reorderable =
              columnReorderEnabled &&
              !header.isPlaceholder &&
              header.column.columnDef.meta?.enableReordering !== false

            return (
              <SortableHeaderCell
                key={header.id}
                reorderable={reorderable}
                header={header}
                resizeGuard={resizeGuard}
                classes={headerCellClasses}
                filterSlot={
                  showHeaderFilterIcon ? renderFilterPopover(header) : undefined
                }
                menuSlot={
                  // Control columns (drag grip, checkbox, actions) opt out of
                  // reordering and have nothing the menu can act on -- its
                  // button would also crowd the narrow header.
                  !header.isPlaceholder &&
                  header.column.columnDef.meta?.enableReordering !== false
                    ? renderColumnMenu(header)
                    : undefined
                }
                thClassName={
                  pinned
                    ? pinnedCellClassNames(pinned, headerPinnedClasses)
                    : undefined
                }
                thStyle={pinnedCellStyle(pinned)}
              />
            )
          })}
          {/* Filler header cell — carries the header band across the
                empty width. */}
          <th
            aria-hidden="true"
            className={`${styles.th} ${styles.fillerTh}`}
          />
        </tr>

        {/* Floating filter row — single-condition editor per column,
              reflecting conditions[0] of the same descriptor. */}
        {showFloatingFilters && (
          <tr
            key={`${leafHeaderGroup.id}-floating`}
            data-role="floating-filters"
            role="row"
            aria-rowindex={groupHeaderRows.length + 2}
          >
            {leafHeaderGroup.headers.map((header) => {
              const column = header.column
              const pinned = getPinnedOffsets(column)
              const filterThClassName = `${styles.filterTh}${
                pinned
                  ? ` ${pinnedCellClassNames(pinned, filterPinnedClasses)}`
                  : ''
              }`
              const filterThStyle = pinnedCellStyle(pinned)
              if (header.isPlaceholder || !column.getCanFilter()) {
                return (
                  <th
                    key={`${header.id}-floating`}
                    role="gridcell"
                    aria-colindex={header.index + 1}
                    className={filterThClassName}
                    style={filterThStyle}
                  />
                )
              }

              const meta = column.columnDef.meta
              const colFilterType = columnFilterType(header)
              const colFilterValue = column.getFilterValue() as
                ColumnFilterModel | undefined

              if (colFilterType === 'set') {
                return (
                  <th
                    key={`${header.id}-floating`}
                    role="gridcell"
                    aria-colindex={header.index + 1}
                    className={filterThClassName}
                    style={filterThStyle}
                    onClick={(e) => e.stopPropagation()}
                  >
                    {renderSetFilter(header)}
                  </th>
                )
              }

              // The floating DatePicker can faithfully edit only a simple
              // equals; any richer date filter shows a read-only summary
              // chip instead. The cell structure must stay identical in
              // both states — left child input ⇄ chip, right child ALWAYS
              // the same popover trigger icon — so the open popover's
              // anchor is never replaced (antd closes it as a click-outside
              // otherwise, flickering when the filter activates).
              const showDateSummary =
                colFilterType === 'date' && !canFloatingEditDate(colFilterValue)

              return (
                <th
                  key={`${header.id}-floating`}
                  role="gridcell"
                  aria-colindex={header.index + 1}
                  className={filterThClassName}
                  style={filterThStyle}
                  onClick={(e) => e.stopPropagation()}
                >
                  <div className={styles.floatingCell}>
                    {showDateSummary ? (
                      <span
                        className={styles.setSummary}
                        title={describeDateFilter(colFilterValue)}
                      >
                        {describeDateFilter(colFilterValue)}
                      </span>
                    ) : (
                      <FloatingFilter
                        filterType={colFilterType}
                        // Only reflect a matching condition descriptor. On
                        // a combined column, a `set` descriptor leaves the
                        // floating input empty (AG Grid behavior).
                        value={
                          colFilterValue?.type === colFilterType
                            ? colFilterValue
                            : undefined
                        }
                        placeholder={meta?.filterPlaceholder}
                        onChange={(next) => column.setFilterValue(next)}
                      />
                    )}
                    {renderFilterPopover(header)}
                  </div>
                </th>
              )
            })}
            {/* Filler cell carries the filter-row band to the edge. */}
            <th
              aria-hidden="true"
              className={`${styles.filterTh} ${styles.fillerTh}`}
            />
          </tr>
        )}
      </thead>
    </table>
  )

  const headerTableContent = columnReorderEnabled ? (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCenter}
      modifiers={[restrictToHorizontalAxis]}
      onDragEnd={handleColumnDragEnd}
    >
      <SortableContext
        items={columnSortableIds}
        strategy={horizontalListSortingStrategy}
      >
        {headerTable}
      </SortableContext>
    </DndContext>
  ) : (
    headerTable
  )

  const tableContent = (
    <div
      className={styles.tableArea}
      role={isTree ? 'treegrid' : 'grid'}
      aria-label={ariaLabel}
      aria-rowcount={headerRowCount + rows.length}
      aria-colcount={orderedVisibleLeafColumns.length}
    >
      <div className={styles.headerArea}>
        <div className={styles.headerViewport} ref={headerViewportRef}>
          {headerTableContent}
        </div>
        {/* Spacer above the grid's vertical scrollbar — same header band
            styling, sized from the live scrollbar measurement. The grid table
            keeps its own scrollbar whether or not a chart pane is present. */}
        <div
          className={styles.headerScrollbarSpacer}
          style={{ width: scrollbarWidth }}
          aria-hidden="true"
        />
        {/* Right-pane header (e.g. the date axis), sized to match the pane so it
            stays aligned above the bar track. Its own horizontal scroll is
            mirrored from the bar track. */}
        {hasRightPane && (
          <>
            <div className={styles.rightPaneDividerSpacer} aria-hidden="true" />
            <div
              ref={rightPaneHeaderRef}
              aria-hidden="true"
              className={styles.rightPaneHeader}
              style={{ width: rightPaneWidth }}
            >
              {rightPane!.header}
            </div>
          </>
        )}
      </div>
      <GridBody
        rows={rows}
        bodyViewportRef={bodyViewportRef}
        onBodyScroll={handleBodyScroll}
        rightPane={rightPane}
        rightPaneWidth={rightPaneWidth}
        onRightPaneDividerDown={onRightPaneDividerDown}
        rightPaneTrackRef={rightPaneTrackRef}
        rightPaneCanvasRef={rightPaneCanvasRef}
        onBarTrackScroll={handleBarTrackScroll}
        colGroup={colGroup}
        isLoading={isLoading}
        emptyMessage={emptyMessage}
        visibleColumnCount={visibleColumnCount}
        columnIds={orderedVisibleLeafColumns.map((column) => column.id)}
        headerRowCount={headerRowCount}
        treeColumnId={
          (
            orderedVisibleLeafColumns.find(
              (column) => column.columnDef.meta?.treeColumn,
            ) ?? orderedVisibleLeafColumns[0]
          )?.id ?? null
        }
        numericColumnIds={numericColumnIds}
        isTree={isTree}
        isGrouped={isGrouped}
        renderGroupHeader={renderGroupHeader}
        flatDndEnabled={flatDndEnabled}
        isDragEnabled={isDragEnabled}
        draggedNodeId={draggedNodeId}
        flatRowId={flatRowId}
        onRowActivate={isTree ? undefined : onRowActivate}
        activatedRowId={activatedRowId}
        getRowActivateLabel={getRowActivateLabel}
        cellEditing={cellEditingEnabled ? cellEditing : undefined}
        runRowAction={hasRowActions ? runRowAction : undefined}
        rowActionSupport={rowActionSupport}
        footerHintsHost={footerHintsHost}
        measuredRowHeight={measuredRowHeight}
        onRowHeightMeasured={setMeasuredRowHeight}
      />
    </div>
  )

  return (
    <div
      ref={gridContainerRef}
      className={styles.grid}
      style={{ height: resolvedHeight }}
    >
      {/* A simple grid has a section heading above it already saying what it
          is, so a toolbar — including its row count — would repeat that and
          compete with the page's own chrome. */}
      {!isSimple && (
        <GridToolbar
          displayedRowCount={displayedRowCount}
          totalRowCount={totalRowCount}
          searchValue={searchValue}
          onSearchChange={onSearchChange}
          onRefresh={onRefresh}
          onClearFilters={onClearFilters}
          hasActiveFilters={hasActiveFilters}
          onExportCsv={includeExportButton ? onExportCsv : undefined}
          isLoading={isLoading}
          includeGlobalSearch={includeGlobalSearch}
          leftSlot={resolvedLeftSlot}
          helpContent={
            helpContent ??
            (cellEditingEnabled || hasRowActions ? (
              <WaydGridKeyboardHelp
                editing={cellEditingEnabled}
                tree={isTree}
                rows={rowActionSupport}
              >
                {helpNotes}
              </WaydGridKeyboardHelp>
            ) : undefined)
          }
          actionsSlot={actionsSlot}
          rightSlot={rightSlot}
        />
      )}

      {dndEnabled ? (
        <DndContext
          sensors={sensors}
          collisionDetection={closestCenter}
          onDragStart={handleDragStart}
          onDragEnd={treeDndEnabled ? handleDragEnd : handleFlatDragEnd}
          onDragCancel={handleDragCancel}
        >
          <SortableContext
            items={
              treeDndEnabled
                ? flattenedNodes.map((t) => t.node.id)
                : rows.map((row) => flatRowId(row))
            }
            strategy={verticalListSortingStrategy}
          >
            {tableContent}
          </SortableContext>
        </DndContext>
      ) : (
        tableContent
      )}
      {/* The grid's footer: a line kept under the frame, so nothing moves
          when it fills. The key hints show here while a cell is selected. */}
      {cellEditingEnabled && (
        <div className={styles.footer} data-grid-footer="">
          <div ref={setFooterHintsHost} className={styles.footerHints} />
        </div>
      )}

      <ColumnChooserModal
        table={table}
        open={isColumnChooserOpen}
        onClose={() => setIsColumnChooserOpen(false)}
        onToggleColumn={handleUserToggleColumn}
        reorderEnabled={columnReorderEnabled}
        onReorderColumn={moveColumn}
      />
    </div>
  )
}

/**
 * The unified Wayd data grid, assembled from the shared grid core (toolbar,
 * header cells, row renderers, table/state hooks, descriptor filter engine,
 * CSV export). Flat by default; provide `getSubRows` to turn on tree mode —
 * expansion, `filterFromLeafRows` (a matching child keeps its ancestor chain
 * visible), and, when configured, reparenting drag-and-drop, cell editing,
 * and draft rows. Flat mode supports row-reorder drag-and-drop via
 * `onRowReorder`. Rows render through the row-renderer seam: the flat forms
 * ({@link FlatGridRow} / {@link SortableFlatGridRow}) or the tree form
 * ({@link TreeGridRow}). Body rows are always virtualized (ag-grid style) —
 * only the visible window plus overscan is mounted.
 */
const WaydGrid = forwardRef(WaydGridInner) as <T extends RowData>(
  props: WaydGridProps<T> & { ref?: Ref<WaydGridHandle> },
) => ReactElement | null

export default WaydGrid
