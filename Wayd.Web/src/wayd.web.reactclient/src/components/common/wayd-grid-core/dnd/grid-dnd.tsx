'use client'

import { KeyboardSensor, PointerSensor, useSensor, useSensors } from '@dnd-kit/core'
import { useSortable } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { createContext, useContext, useMemo, CSSProperties, FC, ReactNode } from 'react'
import { HolderOutlined } from '@ant-design/icons'
import { theme, Tooltip } from 'antd'

// Shared drag MECHANICS for grid drag-and-drop: sensor setup and the sortable
// row wrapper + drag-handle context. Grid-agnostic — the tree-only reparenting
// projection lives in ./tree-projection.

/** Pixels of pointer movement before a drag activates (vs. a click). */
export const DRAG_ACTIVATION_DISTANCE = 8

/**
 * The dnd-kit sensor set shared by all grids: pointer with a small activation
 * distance (so plain clicks don't start drags) plus keyboard.
 */
export function useGridDndSensors() {
  return useSensors(
    useSensor(PointerSensor, {
      activationConstraint: { distance: DRAG_ACTIVATION_DISTANCE },
    }),
    useSensor(KeyboardSensor),
  )
}

// Context to share drag listeners with child components (drag handle)
const GridDragHandleContext = createContext<{
  listeners?: any
  attributes?: any
} | null>(null)

/**
 * Hook to access drag handle listeners and attributes.
 * Must be used within a GridSortableRow.
 */
export function useGridDragHandle() {
  const context = useContext(GridDragHandleContext)
  if (!context) {
    throw new Error('useGridDragHandle must be used within GridSortableRow')
  }
  return context
}

/**
 * The grab handle for a reorderable row.
 *
 * Disabled — greyed, not-allowed, and explained by a tooltip — while the grid is
 * sorted, filtered or searched, because the displayed order is not the data
 * order then and a drop would write a sequence the reader never saw. The grid
 * decides that and passes it down as `context.isDragEnabled`.
 *
 * `disabledTooltip` names the records in the caller's own words, so the reader
 * is told what to clear and what it would let them reorder.
 */
export const DragHandleCell: FC<{
  isDragEnabled: boolean
  disabledTooltip: string
}> = ({ isDragEnabled, disabledTooltip }) => {
  const { token } = theme.useToken()
  const { listeners, attributes } = useGridDragHandle()

  return (
    <Tooltip title={isDragEnabled ? undefined : disabledTooltip}>
      <span
        {...(isDragEnabled ? { ...listeners, ...attributes } : {})}
        style={{
          cursor: isDragEnabled ? 'grab' : 'not-allowed',
          color: isDragEnabled
            ? token.colorTextTertiary
            : token.colorTextDisabled,
          display: 'inline-flex',
          padding: '0 4px',
          touchAction: 'none',
        }}
        aria-label="Drag to reorder"
        aria-disabled={!isDragEnabled}
      >
        <HolderOutlined />
      </span>
    </Tooltip>
  )
}

/**
 * Extends the row's intrinsic attributes so callers can pass through the
 * activation set (`role`, `tabIndex`, `onKeyDown`, `aria-label`) as one spread.
 * Listing them individually meant a new attribute was silently dropped rather
 * than failing to compile.
 */
interface GridSortableRowProps
  extends React.HTMLAttributes<HTMLTableRowElement> {
  nodeId: string
  isDragEnabled: boolean
  isDragging?: boolean
  children: ReactNode
}

/**
 * Sortable table row wrapper for drag-and-drop functionality.
 * Uses @dnd-kit/sortable to make table rows draggable via a drag handle.
 */
export function GridSortableRow({
  nodeId,
  isDragEnabled,
  isDragging: parentIsDragging,
  className = '',
  children,
  ...rowProps
}: GridSortableRowProps) {
  const {
    attributes,
    listeners,
    setNodeRef,
    transform,
    transition,
    isDragging,
  } = useSortable({
    id: nodeId,
    disabled: !isDragEnabled,
  })

  const style: CSSProperties = {
    transform: CSS.Transform.toString(transform),
    transition,
    opacity: isDragging || parentIsDragging ? 0.4 : 1,
    position: isDragging || transform ? 'relative' : undefined,
    zIndex: isDragging ? 999 : undefined,
  }

  const dragHandleContextValue = useMemo(
    () => ({ listeners, attributes }),
    [listeners, attributes],
  )

  return (
    <GridDragHandleContext.Provider value={dragHandleContextValue}>
      <tr
        ref={setNodeRef}
        style={style}
        className={className}
        data-row-id={nodeId}
        data-dragging={isDragging}
        {...rowProps}
      >
        {children}
      </tr>
    </GridDragHandleContext.Provider>
  )
}
