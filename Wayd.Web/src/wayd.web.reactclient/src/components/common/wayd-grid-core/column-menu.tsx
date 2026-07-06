'use client'

import { useState } from 'react'
import { Button, Checkbox, Dropdown, Input, Modal } from 'antd'
import type { MenuProps } from 'antd'
import {
  CheckOutlined,
  HolderOutlined,
  MoreOutlined,
  PushpinOutlined,
  ReloadOutlined,
  SearchOutlined,
  SortAscendingOutlined,
  SortDescendingOutlined,
  ColumnWidthOutlined,
  ControlOutlined,
} from '@ant-design/icons'
import type { ColumnPinningPosition, Header, Table } from '@tanstack/react-table'
import {
  DndContext,
  type DragEndEvent,
  KeyboardSensor,
  PointerSensor,
  closestCenter,
  useSensor,
  useSensors,
} from '@dnd-kit/core'
import {
  SortableContext,
  useSortable,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'

import { getOrderedAllLeafColumns } from './column-order'
import styles from './column-menu.module.css'

/** A leaf column offered in the Choose Columns panel. */
export interface ColumnChooserOption {
  id: string
  label: string
  visible: boolean
}

/** Chooser label: string header → meta.exportHeader → none (excluded). */
const resolveChooserLabel = (columnDef: {
  header?: unknown
  meta?: { exportHeader?: string }
}): string | undefined => {
  if (typeof columnDef.header === 'string' && columnDef.header.trim() !== '') {
    return columnDef.header
  }
  return columnDef.meta?.exportHeader
}

/**
 * The leaf columns the user may show/hide, in on-screen display order (so the
 * checkbox list matches the grid left-to-right, reflecting reorder + pinning).
 * Excludes consumer-controlled columns (`meta.hide === true` — those stay
 * reactive to the consumer's flag), columns with hiding disabled, and
 * structural columns with no displayable label (e.g. the row-actions column).
 */
export function getColumnChooserOptions<T>(
  table: Table<T>,
): ColumnChooserOption[] {
  const options: ColumnChooserOption[] = []
  for (const column of getOrderedAllLeafColumns(table)) {
    if (column.columnDef.meta?.hide === true) continue
    if (!column.getCanHide()) continue
    const label = resolveChooserLabel(column.columnDef)
    if (!label) continue
    options.push({ id: column.id, label, visible: column.getIsVisible() })
  }
  return options
}

/** Everything {@link buildColumnMenuItems} needs, as plain values so the
 *  item-building logic is unit-testable without a table instance. */
export interface ColumnMenuItemsInput {
  canSort: boolean
  sortState: false | 'asc' | 'desc'
  canPin: boolean
  pinnedState: ColumnPinningPosition
  canResize: boolean
  /** Whether the grid has any user-hidable columns (shows Choose Columns). */
  hasHidableColumns: boolean
}

/** Menu item keys (also the switch keys in the click handler). */
export const COLUMN_MENU_KEYS = {
  sortAsc: 'sort-asc',
  sortDesc: 'sort-desc',
  sortClear: 'sort-clear',
  pin: 'pin',
  pinLeft: 'pin-left',
  pinRight: 'pin-right',
  pinNone: 'pin-none',
  autosizeThis: 'autosize-this',
  autosizeAll: 'autosize-all',
  chooseColumns: 'choose-columns',
  reset: 'reset-columns',
} as const

/**
 * Builds the antd menu items for one column's header menu: set-sort items
 * (with Clear Sort while this column is sorted), the Pin Column submenu, the
 * autosize pair, Choose Columns (opens the {@link ColumnChooserModal}), and
 * Reset Columns.
 */
export function buildColumnMenuItems(
  input: ColumnMenuItemsInput,
): MenuProps['items'] {
  const {
    canSort,
    sortState,
    canPin,
    pinnedState,
    canResize,
    hasHidableColumns,
  } = input

  const items: NonNullable<MenuProps['items']> = []

  if (canSort) {
    items.push(
      {
        key: COLUMN_MENU_KEYS.sortAsc,
        label: 'Sort Ascending',
        icon: <SortAscendingOutlined />,
      },
      {
        key: COLUMN_MENU_KEYS.sortDesc,
        label: 'Sort Descending',
        icon: <SortDescendingOutlined />,
      },
    )
    if (sortState) {
      items.push({ key: COLUMN_MENU_KEYS.sortClear, label: 'Clear Sort' })
    }
    items.push({ type: 'divider' })
  }

  if (canPin) {
    const pinCheck = (position: ColumnPinningPosition) =>
      pinnedState === position ? <CheckOutlined /> : undefined
    items.push(
      {
        key: COLUMN_MENU_KEYS.pin,
        label: 'Pin Column',
        icon: <PushpinOutlined />,
        children: [
          {
            key: COLUMN_MENU_KEYS.pinLeft,
            label: 'Pin Left',
            icon: pinCheck('left'),
          },
          {
            key: COLUMN_MENU_KEYS.pinRight,
            label: 'Pin Right',
            icon: pinCheck('right'),
          },
          {
            key: COLUMN_MENU_KEYS.pinNone,
            label: 'No Pin',
            icon: pinCheck(false),
          },
        ],
      },
      { type: 'divider' },
    )
  }

  if (canResize) {
    items.push({
      key: COLUMN_MENU_KEYS.autosizeThis,
      label: 'Autosize This Column',
      icon: <ColumnWidthOutlined />,
    })
  }
  items.push(
    { key: COLUMN_MENU_KEYS.autosizeAll, label: 'Autosize All Columns' },
    { type: 'divider' },
  )

  if (hasHidableColumns) {
    items.push({
      key: COLUMN_MENU_KEYS.chooseColumns,
      label: 'Choose Columns',
      icon: <ControlOutlined />,
    })
  }

  items.push({
    key: COLUMN_MENU_KEYS.reset,
    label: 'Reset Columns',
    icon: <ReloadOutlined />,
  })

  return items
}

export interface ColumnMenuTriggerProps<T> {
  header: Header<T, unknown>
  table: Table<T>
  /** Controlled open state — the grid owns one open-menu column id (same
   *  pattern as the filter popovers) so the trigger's DOM stays stable. */
  open: boolean
  onOpenChange: (open: boolean) => void
  /** Whether the grid has any user-hidable columns — computed ONCE at the
   *  grid level and shared by every trigger (deriving it here would rescan
   *  all leaf columns per header cell, O(N²) across a header render). */
  hasHidableColumns: boolean
  /** Opens the grid's {@link ColumnChooserModal}. */
  onOpenColumnChooser: () => void
  onAutosizeColumn: (columnId: string) => void
  onAutosizeAllColumns: () => void
  onResetColumns: () => void
}

/**
 * The `⋮` column-menu trigger + dropdown for one leaf header cell. Rendered
 * into {@link GridHeaderCell}'s menu slot; visible on header hover, focus,
 * and while open. Sort items SET the sort (single column) — multi-sort stays
 * ctrl/meta-click on the header.
 *
 * Reads TanStack column state (sort/pin/visibility) from a mutable instance —
 * the same staleness hazard as the grid — hence `'use no memo'`.
 */
export function ColumnMenuTrigger<T>({
  header,
  table,
  open,
  onOpenChange,
  hasHidableColumns,
  onOpenColumnChooser,
  onAutosizeColumn,
  onAutosizeAllColumns,
  onResetColumns,
}: ColumnMenuTriggerProps<T>) {
  // eslint-disable-next-line react-compiler/react-compiler -- false-positive "unused directive"; see GridHeaderCell
  'use no memo'
  const column = header.column

  const items = buildColumnMenuItems({
    canSort: column.getCanSort(),
    sortState: column.getIsSorted(),
    canPin: column.getCanPin(),
    pinnedState: column.getIsPinned(),
    canResize: column.getCanResize(),
    hasHidableColumns,
  })

  const handleMenuClick: MenuProps['onClick'] = ({ key, domEvent }) => {
    // The menu portals to document.body, but React events still bubble to the
    // logical parent <th> — never let a menu click toggle the sort.
    domEvent.stopPropagation()

    switch (key) {
      case COLUMN_MENU_KEYS.sortAsc:
        table.setSorting([{ id: column.id, desc: false }])
        break
      case COLUMN_MENU_KEYS.sortDesc:
        table.setSorting([{ id: column.id, desc: true }])
        break
      case COLUMN_MENU_KEYS.sortClear:
        table.setSorting((prev) => prev.filter((s) => s.id !== column.id))
        break
      case COLUMN_MENU_KEYS.pinLeft:
        column.pin('left')
        break
      case COLUMN_MENU_KEYS.pinRight:
        column.pin('right')
        break
      case COLUMN_MENU_KEYS.pinNone:
        column.pin(false)
        break
      case COLUMN_MENU_KEYS.autosizeThis:
        onAutosizeColumn(column.id)
        break
      case COLUMN_MENU_KEYS.autosizeAll:
        onAutosizeAllColumns()
        break
      case COLUMN_MENU_KEYS.chooseColumns:
        onOpenColumnChooser()
        break
      case COLUMN_MENU_KEYS.reset:
        onResetColumns()
        break
      default:
        return
    }
    onOpenChange(false)
  }

  return (
    <Dropdown
      trigger={['click']}
      open={open}
      // Menu-sourced close requests fire on EVERY item click; closing is
      // decided in handleMenuClick instead (chooser toggles keep it open).
      onOpenChange={(nextOpen, info) => {
        if (info.source === 'menu') return
        onOpenChange(nextOpen)
      }}
      menu={{ items, onClick: handleMenuClick }}
      getPopupContainer={() => document.body}
      // Stop popup clicks from bubbling (through the portal) to the <th>
      // sort handler — same guard as the filter popovers.
      popupRender={(menu) => (
        <div onClick={(e) => e.stopPropagation()}>{menu}</div>
      )}
      placement="bottomRight"
    >
      <button
        type="button"
        aria-label="Column menu"
        className={`${styles.trigger}${open ? ` ${styles.triggerOpen}` : ''}`}
        onClick={(e) => e.stopPropagation()}
        // The whole header cell is a column-reorder drag handle; keep a click on
        // the menu button from starting a drag.
        onPointerDown={(e) => e.stopPropagation()}
      >
        <MoreOutlined />
      </button>
    </Dropdown>
  )
}

export interface ColumnChooserModalProps<T> {
  table: Table<T>
  open: boolean
  onClose: () => void
  /** Writes the USER visibility layer for one column. */
  onToggleColumn: (columnId: string, visible: boolean) => void
  /** Whether drag-to-reorder is offered in the list (off for grouped-header
   *  grids, matching header-drag). Default false. */
  reorderEnabled?: boolean
  /** Moves `activeId` to `overId`'s position (section-aware; the grid owns the
   *  rules). Called on a list drop. */
  onReorderColumn?: (activeId: string, overId: string) => void
}

/** One draggable chooser row: a grip, a visibility checkbox, and the label. */
function ChooserRow({
  option,
  reorderEnabled,
  lockedOn,
  onToggle,
}: {
  option: ColumnChooserOption
  reorderEnabled: boolean
  /** Keep the last visible column checked-and-disabled. */
  lockedOn: boolean
  onToggle: (visible: boolean) => void
}) {
  const { setNodeRef, transform, transition, isDragging, listeners, attributes } =
    useSortable({ id: option.id, disabled: !reorderEnabled })
  const style = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging ? 0.5 : undefined,
  }

  return (
    <div ref={setNodeRef} style={style} className={styles.chooserRow}>
      {reorderEnabled && (
        <span
          {...attributes}
          {...listeners}
          role="button"
          aria-label="Reorder column"
          className={styles.chooserDragHandle}
        >
          <HolderOutlined />
        </span>
      )}
      <label className={styles.chooserRowLabel}>
        <Checkbox
          checked={option.visible}
          disabled={lockedOn}
          onChange={(e) => onToggle(e.target.checked)}
        />
        <span className={styles.chooserLabel}>{option.label}</span>
      </label>
    </div>
  )
}

/**
 * The Choose Columns modal (opened from any column menu): a searchable
 * checkbox per user-hidable leaf column. Toggles apply to the grid
 * immediately — the modal stays open so multiple columns can be shown/hidden
 * in one visit; Done just closes it.
 *
 * Reads live column visibility from the mutable TanStack instance — hence
 * `'use no memo'`.
 */
export function ColumnChooserModal<T>({
  table,
  open,
  onClose,
  onToggleColumn,
  reorderEnabled = false,
  onReorderColumn,
}: ColumnChooserModalProps<T>) {
  // eslint-disable-next-line react-compiler/react-compiler -- false-positive "unused directive"; see GridHeaderCell
  'use no memo'
  const [search, setSearch] = useState('')

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 4 } }),
    useSensor(KeyboardSensor),
  )

  const options = getColumnChooserOptions(table)
  const visibleCount = options.filter((col) => col.visible).length
  const query = search.trim().toLowerCase()
  const shownOptions = query
    ? options.filter((col) => col.label.toLowerCase().includes(query))
    : options

  // Reordering is meaningful only over the full, unfiltered list — a filtered
  // subset hides the drop neighbours. Grips show when the feature is on and no
  // search is narrowing the list.
  const canReorder = reorderEnabled && !!onReorderColumn && query === ''

  const handleDragEnd = (event: DragEndEvent) => {
    const { active, over } = event
    if (over && active.id !== over.id) {
      onReorderColumn?.(String(active.id), String(over.id))
    }
  }

  const list = (
    <div className={styles.chooserList}>
      {shownOptions.map((col) => (
        <ChooserRow
          key={col.id}
          option={col}
          reorderEnabled={canReorder}
          // Keep the last visible column locked on — hiding every column would
          // leave an empty table with no header to reopen the chooser from.
          lockedOn={col.visible && visibleCount === 1}
          onToggle={(visible) => onToggleColumn(col.id, visible)}
        />
      ))}
      {shownOptions.length === 0 && (
        <div className={styles.chooserEmpty}>No matches</div>
      )}
    </div>
  )

  return (
    <Modal
      title="Choose Columns"
      open={open}
      onCancel={onClose}
      afterClose={() => setSearch('')}
      width={380}
      footer={
        <Button type="primary" onClick={onClose}>
          Done
        </Button>
      }
    >
      <div className={styles.chooserPanel}>
        <Input
          size="small"
          allowClear
          placeholder="Search columns..."
          prefix={<SearchOutlined />}
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        {canReorder ? (
          <DndContext
            sensors={sensors}
            collisionDetection={closestCenter}
            onDragEnd={handleDragEnd}
          >
            <SortableContext
              items={shownOptions.map((col) => col.id)}
              strategy={verticalListSortingStrategy}
            >
              {list}
            </SortableContext>
          </DndContext>
        ) : (
          list
        )}
      </div>
    </Modal>
  )
}
