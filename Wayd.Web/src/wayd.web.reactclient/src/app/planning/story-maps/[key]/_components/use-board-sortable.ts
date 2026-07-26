'use client'

import { useSortable } from '@dnd-kit/sortable'
import { CSSProperties } from 'react'
import { DropSide } from './board-drag'

/**
 * Shared wiring for a draggable board node (goal, step, task, or swim lane).
 *
 * The whole cell is the drag surface — there is no grab handle. A handle would have to reserve space
 * on the leading edge of every cell, and since it only appears on hover that space is either a
 * permanent gutter indenting every name on the board, or a shift that moves text under the cursor.
 * Neither is acceptable on a grid this dense.
 *
 * The cell's own controls keep working because the pointer sensor needs 4px of movement before a
 * drag begins, so a click still reaches the inline editors, persona dots, and hover actions. Those
 * controls additionally stop propagation where a drag would be actively wrong.
 */
export interface BoardSortableOptions {
  /**
   * Which side of the hovered node the drop lands on, from the board's pointer tracking. Passed in
   * rather than derived from list indices: the sortable list is board-wide while `newOrder` is
   * scoped to a parent, so index comparisons give the wrong answer across a parent boundary — and
   * they cannot distinguish "after the last step of goal 1" from "before the first step of goal 2",
   * which are the same seam.
   */
  dropSide?: DropSide
}

export const useBoardSortable = (
  id: string,
  disabled: boolean,
  { dropSide }: BoardSortableOptions = {},
) => {
  // `transform`/`transition` are intentionally NOT taken from useSortable. Every board node is
  // placed by explicit grid coordinates, so the sorting transforms cannot actually rearrange
  // anything — they only slide cards around visually. Worse, one flat step list spans every goal, so
  // shifting neighbours drags the next goal's first step leftwards under the previous goal's header,
  // which reads as reparenting a step the user never touched. The insertion line shows the landing
  // position instead, and the DragOverlay shows what is being carried.
  const { attributes, listeners, setNodeRef, isDragging, isOver } = useSortable(
    { id, disabled },
  )

  const style: CSSProperties = {
    // Dim the source in place to mark where the node came from.
    opacity: isDragging ? 0.3 : undefined,
  }

  return {
    attributes,
    listeners,
    setNodeRef,
    style,
    isDragging,
    /**
     * This node is the current drop target — i.e. the dragged node would land next to it. Used to
     * draw an insertion line, so a cross-cell drop shows exactly where it will land instead of only
     * highlighting the destination cell.
     */
    isDropTarget: isOver && !isDragging,
    /**
     * Whether the insertion line belongs on this node's trailing edge (below for a task, right for a
     * goal or step) rather than its leading one. Mirrors exactly what the drop will do, because both
     * read the same pointer-derived side.
     */
    dropsAfter: isOver && dropSide === 'after',
  }
}
