'use client'

import { KeyboardSensor, PointerSensor, useSensor, useSensors } from '@dnd-kit/core'
import { useSortable } from '@dnd-kit/sortable'
import { CSS } from '@dnd-kit/utilities'
import { createContext, useContext, useMemo, CSSProperties, ReactNode } from 'react'

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

interface GridSortableRowProps {
  nodeId: string
  isDragEnabled: boolean
  isDragging?: boolean
  className?: string
  onClick?: (e: React.MouseEvent<HTMLTableRowElement>) => void
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
  onClick,
  children,
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
        onClick={onClick}
        data-row-id={nodeId}
        data-dragging={isDragging}
      >
        {children}
      </tr>
    </GridDragHandleContext.Provider>
  )
}
