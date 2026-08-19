'use client'

import { useState, type CSSProperties, type ReactNode } from 'react'
import { Button, Checkbox, Flex } from 'antd'
import {
  DeleteOutlined,
  HolderOutlined,
  RightOutlined,
} from '@ant-design/icons'
import {
  DndContext,
  DragOverlay,
  useDraggable,
  useDroppable,
  type DragEndEvent,
} from '@dnd-kit/core'
import type { ColumnDef } from './wayd-grid-core'
import { useGridDndSensors } from './wayd-grid-core/dnd/grid-dnd'
import WaydGrid from './wayd-grid/wayd-grid'
import WaydTooltip from './wayd-tooltip'
import type { RowData, RowSelectionState } from '@tanstack/react-table'

const DEFAULT_HEIGHT = 400
const CONTROL_COLUMN_SIZE = 44
const DRAG_COLUMN_SIZE = 36
const DROP_ZONE_ID = 'wayd-grid-transfer-target'

/**
 * The rows a drag moves: dragging a checked row brings every checked row
 * along (ag-grid multi-row drag behavior); dragging an unchecked row moves
 * just that row and leaves the selection alone. Ids that are no longer in
 * `leftData` (moved earlier, or dropped from fresh search results) are
 * ignored.
 */
export const resolveDragItems = <TData extends RowData,>(
  draggedId: string,
  leftData: TData[],
  selectedIds: ReadonlySet<string>,
  getRowId: (item: TData) => string,
): TData[] => {
  const dragged = leftData.find((item) => getRowId(item) === draggedId)
  if (!dragged) return []
  if (!selectedIds.has(draggedId)) return [dragged]
  return leftData.filter((item) => selectedIds.has(getRowId(item)))
}

/** Drag-handle cell for a left-grid row (dnd-kit draggable source). */
const TransferDragHandle = ({ id }: { id: string }) => {
  const { attributes, listeners, setNodeRef } = useDraggable({ id })

  return (
    <span
      ref={setNodeRef}
      {...attributes}
      {...listeners}
      aria-label="Drag to move"
      style={{
        cursor: 'grab',
        touchAction: 'none',
        color: 'var(--ant-color-text-tertiary)',
      }}
    >
      <HolderOutlined />
    </span>
  )
}

/** Drop target wrapping the right grid; highlights while a row hovers it. */
const TransferDropZone = ({ children }: { children: ReactNode }) => {
  const { isOver, setNodeRef } = useDroppable({ id: DROP_ZONE_ID })

  const style: CSSProperties = {
    flex: 1,
    minWidth: 0,
    borderRadius: 6,
    outline: isOver ? '2px dashed var(--ant-color-primary)' : undefined,
    outlineOffset: 2,
  }

  return (
    <div ref={setNodeRef} style={style} data-transfer-drop-zone>
      {children}
    </div>
  )
}

export interface WaydGridTransferProps<TData extends RowData> {
  /** Rows available to pick from (the left grid). */
  leftData?: TData[]
  /** Rows already chosen (the right grid). */
  rightData?: TData[]
  /**
   * Column definitions rendered by both grids. The transfer prepends its own
   * control columns — a drag handle and selection checkbox on the left grid
   * and a remove icon on the right grid.
   */
  columns: ColumnDef<TData, any>[]
  /** Stable row id, used for row identity and selection tracking. */
  getRowId: (item: TData) => string
  /** Called with the moved rows when they are dragged onto the right grid or
   *  the move button is clicked. */
  onMove: (items: TData[]) => void
  /** Called when a right row's remove icon is clicked. */
  onRemove: (item: TData) => void
  /** Label for the drag overlay while dragging a single row (e.g. its key).
   *  Defaults to `getRowId`. Multi-row drags show "N items". */
  getDragLabel?: (item: TData) => ReactNode
  /** Fixed height in pixels for each grid. Default 400. */
  height?: number
}

/**
 * Dual-list transfer picker: two side-by-side WaydGrid instances. Rows move
 * right by dragging their handle onto the right grid — dragging a checked row
 * brings all checked rows — or by checking rows and clicking the move button;
 * right rows are removed via a delete icon. Selection and drag state live
 * here — the grid core has no row-selection model, and the cross-grid
 * DndContext must span both grids so it cannot live inside either one.
 */
const WaydGridTransfer = <TData extends RowData,>(props: WaydGridTransferProps<TData>) => {
  const {
    leftData = [],
    rightData = [],
    columns,
    getRowId,
    onMove,
    onRemove,
    getDragLabel = (item) => getRowId(item),
    height = DEFAULT_HEIGHT,
  } = props

  // TanStack owns selection (see WaydGrid's `rowSelection` prop): select-all
  // is filtered-scoped and selections survive a filter change, which is the
  // behavior this transfer needs.
  const [rowSelection, setRowSelection] = useState<RowSelectionState>({})
  const [draggingId, setDraggingId] = useState<string | null>(null)

  const sensors = useGridDndSensors()

  // Intersect with leftData so ids that left the grid (moved, or dropped from
  // fresh search results) never count toward the move button or payload.
  const selectedIds = new Set(
    Object.entries(rowSelection)
      .filter(([, isSelected]) => isSelected)
      .map(([id]) => id),
  )
  const selectedItems = leftData.filter((item) =>
    selectedIds.has(getRowId(item)),
  )

  const moveItems = (items: TData[]) => {
    if (items.length === 0) return
    setRowSelection((prev) => {
      const next = { ...prev }
      for (const item of items) delete next[getRowId(item)]
      return next
    })
    onMove(items)
  }

  const handleMoveButton = () => moveItems(selectedItems)

  const handleDragEnd = (event: DragEndEvent) => {
    setDraggingId(null)
    if (event.over?.id !== DROP_ZONE_ID) return
    moveItems(
      resolveDragItems(String(event.active.id), leftData, selectedIds, getRowId),
    )
  }

  const dragItems = draggingId
    ? resolveDragItems(draggingId, leftData, selectedIds, getRowId)
    : []

  const leftColumns: ColumnDef<TData, any>[] = [
    {
      id: 'transfer-drag',
      size: DRAG_COLUMN_SIZE,
      enableSorting: false,
      enableColumnFilter: false,
      header: () => null,
      cell: ({ row }) => <TransferDragHandle id={getRowId(row.original)} />,
    },
    {
      id: 'transfer-select',
      size: CONTROL_COLUMN_SIZE,
      enableSorting: false,
      enableColumnFilter: false,
      header: ({ table }) => (
        <Checkbox
          aria-label="Select all rows"
          checked={table.getIsAllRowsSelected()}
          // getIsSomeRowsSelected() is "at least one" in v9, so it is true
          // when all are selected too -- exclude that for the dash state.
          indeterminate={
            !table.getIsAllRowsSelected() && table.getIsSomeRowsSelected()
          }
          onChange={() => table.toggleAllRowsSelected()}
        />
      ),
      cell: ({ row }) => (
        <Checkbox
          aria-label="Select row"
          checked={row.getIsSelected()}
          onChange={() => row.toggleSelected()}
        />
      ),
    },
    ...columns,
  ]

  const rightColumns: ColumnDef<TData, any>[] = [
    {
      id: 'transfer-remove',
      size: CONTROL_COLUMN_SIZE,
      enableSorting: false,
      enableColumnFilter: false,
      header: () => null,
      cell: ({ row }) => (
        <Button
          type="text"
          size="small"
          aria-label="Remove row"
          icon={<DeleteOutlined />}
          onClick={() => onRemove(row.original)}
        />
      ),
    },
    ...columns,
  ]

  return (
    <DndContext
      sensors={sensors}
      // Never auto-scroll horizontally: dragging a row toward the right grid
      // sweeps past the left wrapper's edge, which would drag its horizontal
      // scrollbar along for the ride. Vertical auto-scroll stays on.
      autoScroll={{ threshold: { x: 0, y: 0.2 } }}
      onDragStart={(event) => setDraggingId(String(event.active.id))}
      onDragEnd={handleDragEnd}
      onDragCancel={() => setDraggingId(null)}
    >
      <Flex gap="small" style={{ width: '100%' }}>
        <div style={{ flex: 1, minWidth: 0 }}>
          <WaydGrid
            data={leftData}
            columns={leftColumns}
            getRowId={getRowId}
            height={height}
            includeGlobalSearch={false}
            includeExportButton={false}
            rowSelection={rowSelection}
            onRowSelectionChange={setRowSelection}
          />
        </div>
        <Flex vertical justify="center">
          <WaydTooltip title="Move selected items">
            <Button
              aria-label="Move selected items"
              icon={<RightOutlined />}
              onClick={handleMoveButton}
              disabled={selectedItems.length === 0}
            />
          </WaydTooltip>
        </Flex>
        <TransferDropZone>
          <WaydGrid
            data={rightData}
            columns={rightColumns}
            getRowId={getRowId}
            height={height}
            includeGlobalSearch={false}
            includeExportButton={false}
          />
        </TransferDropZone>
      </Flex>
      <DragOverlay dropAnimation={null}>
        {dragItems.length > 0 && (
          <div
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: 6,
              padding: '4px 10px',
              borderRadius: 6,
              background: 'var(--ant-color-bg-elevated)',
              border: '1px solid var(--ant-color-border)',
              boxShadow: 'var(--ant-box-shadow-secondary)',
              cursor: 'grabbing',
            }}
          >
            <HolderOutlined />
            {dragItems.length > 1
              ? `${dragItems.length} items`
              : getDragLabel(dragItems[0])}
          </div>
        )}
      </DragOverlay>
    </DndContext>
  )
}

export default WaydGridTransfer
