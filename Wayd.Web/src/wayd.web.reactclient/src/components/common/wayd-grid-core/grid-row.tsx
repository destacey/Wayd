'use client'

import type { Cell, Row } from './index'
import { flexRender } from '@tanstack/react-table'
import { GridSortableRow } from './dnd/grid-dnd'
import {
  getPinnedOffsets,
  pinnedCellClassNames,
  pinnedCellStyle,
  type PinnedCellClasses,
} from './column-pinning'
import type { RowData } from '@tanstack/react-table'
import type { ReactNode } from 'react'
import type { CellMark } from './cell-editing/types'
import { cellMessageId } from './cell-editing/edit-state'
import WaydTooltip from '../wayd-tooltip'

/**
 * CSS-module class names the owning grid supplies for body rows, so the shared
 * markup picks up the grid's own zebra/cell styling. The pinned set is
 * optional — a grid without column pinning just omits it.
 */
export interface GridRowClasses {
  tr: string
  trAlt: string
  td: string
  /** Applied to `td`s of numeric columns (right-aligned cells; headers are
   *  unaffected — alignment is a body-cell concern only). */
  tdNumeric?: string
  /** Sticky/edge classes for pinned columns' `td`s. */
  pinned?: PinnedCellClasses
  /** Applied to a row that can be activated (pointer affordance). */
  trActivatable?: string
  /**
   * Applied to the currently activated row. Distinct from `trSelected`, which
   * marks the row being inline-edited and deliberately reads as *unstyled* —
   * this one is a highlight.
   */
  trActivated?: string
  /** Marks for cells with an edit the server does not have yet. */
  edit?: GridCellEditClasses
}

/** Classes for a cell's edit state. */
export interface GridCellEditClasses {
  unsaved: string
  saving: string
  error: string
  /** The cell whose editor is open. */
  editing: string
}

/**
 * What every body row form needs to take part in the grid's keyboard model:
 * its place in the grid for assistive tech, and which of its cells (if any)
 * holds the grid's single Tab stop.
 */
export interface GridRowNavigationProps {
  /** 1-based row position across the whole grid, header rows included —
   *  rows are virtualized, so the DOM alone cannot say where a row sits. */
  ariaRowIndex: number
  /**
   * The column whose cell is the grid's Tab stop (`tabIndex=0`), or `null`
   * when the Tab stop is on another row. Every other cell is `tabIndex=-1`:
   * reachable by arrow keys, skipped by Tab.
   */
  focusColumnId: string | null
}

/**
 * What a row needs to show cell editing: which of its cells have an edit in
 * flight or failed, and the editor open in one of them. Passed as plain
 * values so a compiled row re-renders exactly when its own cells change.
 */
export interface GridRowEditProps {
  /** This row's cell marks by column id. */
  cellMarks?: Readonly<Record<string, CellMark>>
  /** The column whose editor is open in this row. */
  editColumnId?: string | null
  /** The open editor, shown in place of that cell's content. */
  editor?: ReactNode
  /** The column whose error is shown while its cell has focus. */
  errorTipColumnId?: string | null
  /** Builds the id of the element (outside the table) describing a marked
   *  cell, for `aria-describedby`. */
  editIdPrefix?: string
  /** The row's data id, which those ids are built from. */
  editRowKey?: string
}

/** The class suffix and attributes for a cell's edit state. */
const cellEditProps = (
  columnId: string,
  { cellMarks, editColumnId, editIdPrefix, editRowKey }: GridRowEditProps,
  classes: GridRowClasses,
): {
  className: string
  attrs: React.TdHTMLAttributes<HTMLTableCellElement> & {
    'data-edit-state'?: string
  }
} => {
  const mark = cellMarks?.[columnId]
  const editing =
    editColumnId === columnId && classes.edit ? ` ${classes.edit.editing}` : ''
  if (!mark) return { className: editing, attrs: {} }
  return {
    className: `${classes.edit ? ` ${classes.edit[mark.phase]}` : ''}${editing}`,
    attrs: {
      'data-edit-state': mark.phase,
      'aria-busy': mark.phase === 'saving' || undefined,
      'aria-invalid': mark.phase === 'error' || undefined,
      'aria-describedby':
        editIdPrefix && editRowKey
          ? cellMessageId(editIdPrefix, editRowKey, columnId)
          : undefined,
    },
  }
}

/**
 * A body cell's content: its editor while open, else the column's cell. A
 * failed cell's message shows on hover, and while the cell has focus, so a
 * keyboard user sees why too.
 */
const cellContent = <T extends RowData>(
  cell: Cell<T, unknown>,
  { editColumnId, editor, cellMarks, errorTipColumnId }: GridRowEditProps,
): ReactNode => {
  if (editColumnId === cell.column.id && editor) return editor
  const content = flexRender(cell.column.columnDef.cell, cell.getContext())
  const error = cellMarks?.[cell.column.id]?.error
  if (!error) return content
  return (
    <WaydTooltip
      title={error}
      open={errorTipColumnId === cell.column.id ? true : undefined}
    >
      <span style={ERROR_ANCHOR_STYLE}>{content}</span>
    </WaydTooltip>
  )
}

const ERROR_ANCHOR_STYLE = {
  display: 'block',
  overflow: 'hidden',
  textOverflow: 'ellipsis',
} as const

/** The pinned class suffix (starting with a space) + inline style for a body
 *  cell, or empties when the column is unpinned / pinning classes not given. */
const pinnedTdProps = <T extends RowData>(
  cell: Cell<T, unknown>,
  classes: GridRowClasses,
): { className: string; style?: React.CSSProperties } => {
  if (!classes.pinned) return { className: '' }
  const offsets = getPinnedOffsets(cell.column)
  if (!offsets) return { className: '' }
  return {
    className: ` ${pinnedCellClassNames(offsets, classes.pinned)}`,
    style: pinnedCellStyle(offsets),
  }
}

/** The numeric-alignment class suffix (starting with a space) for a body
 *  cell, or '' when the column isn't numeric / no class was supplied. */
const numericTdClass = <T extends RowData>(
  cell: Cell<T, unknown>,
  classes: GridRowClasses,
  numericColumnIds: ReadonlySet<string> | undefined,
): string =>
  classes.tdNumeric && numericColumnIds?.has(cell.column.id)
    ? ` ${classes.tdNumeric}`
    : ''

/**
 * The grid-cell attributes every body `td` carries: its role and column
 * position, and the roving tab stop. `data-grid-cell` is how the body's key
 * handler tells a focused cell from focused content inside one.
 */
const gridCellProps = (
  columnId: string,
  colIndex: number,
  focusColumnId: string | null,
) => ({
  role: 'gridcell',
  'aria-colindex': colIndex + 1,
  tabIndex: columnId === focusColumnId ? 0 : -1,
  'data-grid-cell': '',
})

/** The grid-row attributes every body `tr` carries. `data-grid-row` holds the
 *  TanStack row id the body uses to find a row's cells again after render. */
const gridRowProps = <T extends RowData>(
  row: Row<T>,
  ariaRowIndex: number,
) => ({
  role: 'row',
  'aria-rowindex': ariaRowIndex,
  'data-grid-row': row.id,
})

const INTERACTIVE_SELECTOR =
  'a, button, input, select, textarea, [role="button"], [data-row-activate="ignore"]'

/**
 * True when the event landed on something inside the row that handles its own
 * activation — a link, a button, an input, or anything that opted out with
 * `data-row-activate="ignore"`.
 *
 * Without this a row-activating grid steals every click from the cells inside
 * it: the actions dropdown would open the panel behind itself, and a link cell
 * would both navigate and activate.
 *
 * The search is bounded by `currentTarget` so content outside the row never
 * counts.
 */
const isInteractiveTarget = (
  target: EventTarget | null,
  row: EventTarget & Element,
): boolean => {
  if (!(target instanceof Element)) return false
  const hit = target.closest(INTERACTIVE_SELECTOR)
  return !!hit && hit !== row && row.contains(hit)
}

/**
 * The zebra + activation class string for a flat body row. Shared by both flat
 * forms so the plain and sortable rows never drift apart.
 */
const flatRowClassName = (
  classes: GridRowClasses,
  index: number,
  { onActivate, isActivated }: RowActivationProps,
): string =>
  `${classes.tr}` +
  `${index % 2 === 1 ? ` ${classes.trAlt}` : ''}` +
  `${onActivate && classes.trActivatable ? ` ${classes.trActivatable}` : ''}` +
  `${isActivated && classes.trActivated ? ` ${classes.trActivated}` : ''}`

/** Row props shared by every activatable row form. */
export interface RowActivationProps {
  /**
   * Called when the row is activated — clicked, or Enter/Space on one of its
   * focused cells (the grid body handles the keys).
   */
  onActivate?: () => void
  /** Whether this row is the activated one (drives the highlight). */
  isActivated?: boolean
  /** Accessible name for the row, e.g. the record's name. */
  activateLabel?: string
}

/**
 * The click handler and label that make a `<tr>` activatable, or nothing at all
 * when the grid has no `onRowActivate`.
 */
const activationProps = ({
  onActivate,
  activateLabel,
}: RowActivationProps): React.HTMLAttributes<HTMLTableRowElement> => {
  if (!onActivate) return {}
  return {
    'aria-label': activateLabel,
    onClick: (e) => {
      if (isInteractiveTarget(e.target, e.currentTarget)) return
      onActivate()
    },
  }
}

export interface FlatGridRowProps<T extends RowData>
  extends RowActivationProps, GridRowNavigationProps, GridRowEditProps {
  row: Row<T>
  /** Display index within the rendered row list (drives zebra striping). */
  index: number
  classes: GridRowClasses
  /** Column ids whose cells right-align (numeric columns); resolved once at
   *  the grid level — see the grid's numericColumnIds. */
  numericColumnIds?: ReadonlySet<string>
}

/**
 * The FLAT form of the row-renderer seam: a plain `<tr>` of the row's visible
 * cells plus the trailing filler cell that keeps the zebra/hover band
 * edge-to-edge. Tree mode supplies its own form (sortable-row wrapper +
 * indent/caret + editable-cell attributes) behind the same seam — do not
 * thread `if (isTree)` through this one.
 *
 * Safe to memoize: v9 hands back a new `table` reference on every state
 * change (and new row objects for rows that actually changed), so compiler
 * memoization invalidates correctly. Under v8 these were long-lived mutating
 * objects, which is why this tree used to be opted out with `'use no memo'`.
 */
export function FlatGridRow<T extends RowData>({
  row,
  index,
  classes,
  numericColumnIds,
  onActivate,
  isActivated,
  activateLabel,
  ariaRowIndex,
  focusColumnId,
  ...edit
}: FlatGridRowProps<T>) {
  return (
    <tr
      className={flatRowClassName(classes, index, {
        onActivate,
        isActivated,
      })}
      {...gridRowProps(row, ariaRowIndex)}
      {...activationProps({ onActivate, activateLabel })}
    >
      {row.getVisibleCells().map((cell, colIndex) => {
        const pinned = pinnedTdProps(cell, classes)
        const editState = cellEditProps(cell.column.id, edit, classes)
        return (
          <td
            key={cell.id}
            data-column-id={cell.column.id}
            className={`${classes.td}${numericTdClass(cell, classes, numericColumnIds)}${pinned.className}${editState.className}`}
            style={pinned.style}
            {...gridCellProps(cell.column.id, colIndex, focusColumnId)}
            {...editState.attrs}
          >
            {cellContent(cell, edit)}
          </td>
        )
      })}
      {/* Filler data cell keeps the zebra/hover band edge-to-edge. */}
      <td aria-hidden="true" className={classes.td} />
    </tr>
  )
}

export interface SortableFlatGridRowProps<
  T extends RowData,
> extends FlatGridRowProps<T> {
  /** The row's data id (not TanStack's row.id) — the dnd-kit sortable id. */
  nodeId: string
  /** Whether this row is currently being dragged. */
  isDragging: boolean
  /** Whether DnD is enabled for this row. */
  isDragEnabled: boolean
}

/**
 * The flat form wrapped in the dnd-kit sortable row ({@link GridSortableRow})
 * for flat row-reorder DnD. Rendered (with dragging possibly disabled) for
 * every row whenever the grid has `onRowReorder`, so drag-handle cells can
 * always reach the `useGridDragHandle` context.
 *
 */
export function SortableFlatGridRow<T extends RowData>({
  row,
  index,
  classes,
  numericColumnIds,
  nodeId,
  isDragging,
  isDragEnabled,
  onActivate,
  isActivated,
  activateLabel,
  ariaRowIndex,
  focusColumnId,
  ...edit
}: SortableFlatGridRowProps<T>) {
  return (
    <GridSortableRow
      nodeId={nodeId}
      isDragEnabled={isDragEnabled}
      isDragging={isDragging}
      className={flatRowClassName(classes, index, {
        onActivate,
        isActivated,
      })}
      {...gridRowProps(row, ariaRowIndex)}
      {...activationProps({ onActivate, activateLabel })}
    >
      {row.getVisibleCells().map((cell, colIndex) => {
        const pinned = pinnedTdProps(cell, classes)
        const editState = cellEditProps(cell.column.id, edit, classes)
        return (
          <td
            key={cell.id}
            data-column-id={cell.column.id}
            className={`${classes.td}${numericTdClass(cell, classes, numericColumnIds)}${pinned.className}${editState.className}`}
            style={pinned.style}
            {...gridCellProps(cell.column.id, colIndex, focusColumnId)}
            {...editState.attrs}
          >
            {cellContent(cell, edit)}
          </td>
        )
      })}
      {/* Filler data cell keeps the zebra/hover band edge-to-edge. */}
      <td aria-hidden="true" className={classes.td} />
    </GridSortableRow>
  )
}

/** Additional row classes tree mode needs on top of the flat set. */
export interface TreeGridRowClasses extends GridRowClasses {
  trEditable: string
  trSelected: string
  editableCell: string
}

export interface TreeGridRowProps<T extends RowData>
  extends GridRowNavigationProps, GridRowEditProps {
  row: Row<T>
  /** Display index within the rendered row list (drives zebra striping). */
  index: number
  classes: TreeGridRowClasses
  /** Column ids whose cells right-align (numeric columns). */
  numericColumnIds?: ReadonlySet<string>
  /** The row's data id (not TanStack's row.id). */
  nodeId: string
  /** Whether this row is selected for inline editing. */
  isSelected: boolean
  /**
   * Whether the row is expanded. Passed in rather than read off `row`: the
   * compiled component can reuse its output while `row` is the same object,
   * and a collapse read inside it was seen to leave `aria-expanded` stale.
   */
  isExpanded: boolean
  /** Whether this row is currently being dragged. */
  isDragging: boolean
  /** Whether DnD is enabled for this row. */
  isDragEnabled: boolean
  /** Whether the grid is editable at all (cursor affordance). */
  canEdit: boolean
  /** Column ids editable for the selected row. */
  editableColumns: string[]
  onRowClick: (e: React.MouseEvent, rowId: string) => void
  onCellClick: (e: React.MouseEvent) => void
}

/**
 * The TREE form of the row-renderer seam: wraps the row in the dnd-kit
 * sortable context ({@link GridSortableRow}) and adds the editing affordances
 * (`data-cell-id` per cell, editable-cell styling on the selected row).
 * Indentation and the expand caret are rendered by the caller's columns
 * (via `row.depth` / `row.getCanExpand()`), not here.
 *
 */
export function TreeGridRow<T extends RowData>({
  row,
  index,
  classes,
  numericColumnIds,
  nodeId,
  isSelected,
  isExpanded,
  isDragging,
  isDragEnabled,
  canEdit,
  editableColumns,
  onRowClick,
  onCellClick,
  ariaRowIndex,
  focusColumnId,
  ...edit
}: TreeGridRowProps<T>) {
  return (
    <GridSortableRow
      nodeId={nodeId}
      isDragEnabled={isDragEnabled}
      isDragging={isDragging}
      className={`${classes.tr}${canEdit ? ` ${classes.trEditable}` : ''}${
        index % 2 === 1 ? ` ${classes.trAlt}` : ''
      }${isSelected ? ` ${classes.trSelected}` : ''}`}
      {...gridRowProps(row, ariaRowIndex)}
      aria-level={row.depth + 1}
      aria-expanded={row.getCanExpand() ? isExpanded : undefined}
      onClick={(e) => onRowClick(e, nodeId)}
    >
      {row.getVisibleCells().map((cell, colIndex) => {
        const isEditableCell =
          isSelected && editableColumns.includes(cell.column.id)
        const pinned = pinnedTdProps(cell, classes)
        const editState = cellEditProps(cell.column.id, edit, classes)

        return (
          <td
            key={cell.id}
            data-cell-id={`${nodeId}-${cell.column.id}`}
            data-column-id={cell.column.id}
            className={`${classes.td}${numericTdClass(cell, classes, numericColumnIds)}${
              isEditableCell ? ` ${classes.editableCell}` : ''
            }${pinned.className}${editState.className}`}
            style={pinned.style}
            onClick={onCellClick}
            {...gridCellProps(cell.column.id, colIndex, focusColumnId)}
            {...editState.attrs}
          >
            {cellContent(cell, edit)}
          </td>
        )
      })}
      {/* Filler data cell keeps the zebra/hover band edge-to-edge. */}
      <td aria-hidden="true" className={classes.td} />
    </GridSortableRow>
  )
}

export interface GroupGridRowClasses {
  tr: string
  trGroup: string
  td: string
  /** The single spanning cell. Stays a table cell, or its colSpan is lost. */
  groupCell: string
  /** The flex line inside it: toggle, indent and heading. */
  groupCellInner: string
  groupToggle: string
  groupToggleExpanded: string
  groupLabel: string
  groupCount: string
}

export interface GroupGridRowProps<
  T extends RowData,
> extends GridRowNavigationProps {
  row: Row<T>
  classes: GroupGridRowClasses
  /** Column count the heading spans (visible leaf columns + the filler). */
  colSpan: number
  /** The heading content; the default is the value and a row count. */
  header?: React.ReactNode
  /** Whether the group is expanded; see `TreeGridRowProps.isExpanded`. */
  isExpanded: boolean
}

/** Pixels of indent per nesting level, so nested groups read as nested. */
const GROUP_INDENT_PER_DEPTH = 20

/**
 * The GROUP form of the row-renderer seam: one cell spanning the grid that
 * carries the expand toggle and the group heading. It is a heading, not a
 * record, so it neither activates nor drags — the toggle is its only control,
 * and the keyboard reaches it through the cell (Enter, Space, → and ←).
 */
export function GroupGridRow<T extends RowData>({
  row,
  classes,
  colSpan,
  header,
  ariaRowIndex,
  focusColumnId,
  isExpanded: expanded,
}: GroupGridRowProps<T>) {
  const count = row.getLeafRows().length
  const label = header ?? (
    <>
      <span className={classes.groupLabel}>
        {String(row.groupingValue ?? '')}
      </span>
      <span className={classes.groupCount}>
        {count} {count === 1 ? 'row' : 'rows'}
      </span>
    </>
  )

  return (
    <tr
      className={`${classes.tr} ${classes.trGroup}`}
      data-group-row="true"
      {...gridRowProps(row, ariaRowIndex)}
      aria-expanded={expanded}
    >
      <td
        className={`${classes.td} ${classes.groupCell}`}
        colSpan={colSpan}
        role="gridcell"
        aria-colindex={1}
        aria-colspan={colSpan - 1}
        // The heading has one cell, so it holds the Tab stop whichever
        // column the cursor was in.
        tabIndex={focusColumnId === null ? -1 : 0}
        data-grid-cell=""
      >
        {/* The flex line is a child: a `display: flex` td is no longer a table
            cell, so its colSpan is ignored and it shrinks to the first column. */}
        <div className={classes.groupCellInner}>
          <button
            type="button"
            tabIndex={-1}
            className={`${classes.groupToggle}${expanded ? ` ${classes.groupToggleExpanded}` : ''}`}
            style={{ marginLeft: row.depth * GROUP_INDENT_PER_DEPTH }}
            aria-expanded={expanded}
            aria-label={expanded ? 'Collapse group' : 'Expand group'}
            onClick={row.getToggleExpandedHandler()}
          >
            <svg
              width="10"
              height="10"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="3"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <path d="M9 6l6 6-6 6" />
            </svg>
          </button>
          {label}
        </div>
      </td>
    </tr>
  )
}
