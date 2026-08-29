'use client'

import { useSortable } from '@dnd-kit/sortable'
import { CSSProperties } from 'react'
import { DropSide } from './board-drag'
import styles from '@/src/app/planning/story-maps/_components/story-map.module.css'

export interface BoardSortableOptions {
  /** Which side of the hovered node a drop lands on, from the board's pointer tracking. */
  dropSide?: DropSide
}

/**
 * Shared wiring for a draggable board node (goal, step, task, or swim lane). The whole cell is the
 * drag surface — there is no grab handle; the pointer sensor's activation distance is what lets a
 * click still reach the inline editors, persona dots, and hover actions.
 */
export const useBoardSortable = (
  id: string,
  disabled: boolean,
  { dropSide }: BoardSortableOptions = {},
) => {
  // `transform`/`transition` are deliberately not taken from useSortable. Board nodes are placed by
  // explicit grid coordinates, so sorting transforms cannot rearrange anything — they only slide
  // cards visually, and because one flat step list spans every goal, that drags a neighbouring
  // goal's first step under the wrong header. The insertion line shows the landing position instead.
  const { attributes, listeners, setNodeRef, isDragging, isOver } = useSortable(
    { id, disabled },
  )

  const style: CSSProperties = {
    opacity: isDragging ? 0.3 : undefined,
  }

  // dnd-kit announces the node as an operable button for its keyboard sensor, which is not
  // registered here — so role/tabIndex would promise an interaction that does not exist, on a cell
  // that already contains real controls (inline editor, persona dots, hover actions).
  const { role: _role, tabIndex: _tabIndex, ...dragAttributes } = attributes

  return {
    attributes: dragAttributes,
    listeners,
    setNodeRef,
    style,
    isDragging,
    /**
     * Marks the cell as a drag surface: grab cursor, and the `touch-action: none` the pointer sensor
     * needs so a touch drag is not stolen by page scrolling. Empty when dragging is disabled.
     */
    dragClassName: disabled ? '' : styles.draggable,
    /** This node is the current drop target, so it draws the insertion line. */
    isDropTarget: isOver && !isDragging,
    /** Draw that line on the trailing edge — below for a task, right for a goal or step. */
    dropsAfter: isOver && dropSide === 'after',
  }
}
