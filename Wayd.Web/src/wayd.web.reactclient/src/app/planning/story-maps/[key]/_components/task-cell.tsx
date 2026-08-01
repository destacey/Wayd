'use client'

import { StoryMapTaskDto } from '@/src/services/wayd-api'
import { useDroppable } from '@dnd-kit/core'
import { SortableContext, verticalListSortingStrategy } from '@dnd-kit/sortable'
import { CSSProperties, FC } from 'react'
import { BoardActions } from './board-actions'
import { DropSide } from './board-drag'
import TaskCard from './task-card'
import styles from '../../_components/story-map.module.css'

export interface TaskCellProps {
  tasks: StoryMapTaskDto[]
  /** Identifies this (step × swim lane) cell as a drop target. */
  cellId: string
  /** 1-based grid column of the owning step. */
  column: number
  /** 1-based grid row of the owning swim lane. */
  row: number
  selectedPersonaId: string | null
  actions: BoardActions
  /** Cells in the right-most column drop the border that would double against the grid's own. */
  isLastColumn: boolean
  /** Which edge of the hovered node a drop lands on, from the board's pointer tracking. */
  dropSide: DropSide
}

/**
 * One cell of the task grid: a step column crossed with a swim-lane row. Renders even when empty, so
 * the grid keeps its borders and the cell stays a valid drop target.
 */
const TaskCell: FC<TaskCellProps> = ({
  tasks,
  cellId,
  column,
  row,
  selectedPersonaId,
  actions,
  isLastColumn,
  dropSide,
}) => {
  // The cell itself is a drop target, not just its cards — an empty cell has no sortable items to
  // aim at. dnd-kit reports isOver on the innermost target only, so a cell and one of its cards
  // never highlight at once.
  const { setNodeRef, isOver } = useDroppable({
    id: cellId,
    disabled: !actions.canUpdate,
  })

  const style: CSSProperties = { gridRow: row, gridColumn: column }

  // Hovering the empty space below the last card targets the cell, which means "append". Show that
  // as an insertion line under the last card rather than only glowing the cell, so every drop uses
  // the same signal. An empty cell has no card to draw on, so it keeps the glow.
  const appendsToEnd = isOver && tasks.length > 0

  return (
    <div
      ref={setNodeRef}
      className={`${styles.taskCell} ${isLastColumn ? styles.lastColumn : ''} ${
        isOver && !appendsToEnd ? styles.taskCellOver : ''
      }`}
      style={style}
    >
      <SortableContext
        items={tasks.map((t) => t.id)}
        strategy={verticalListSortingStrategy}
      >
        {tasks.map((task, i) => (
          <TaskCard
            key={task.id}
            task={task}
            actions={actions}
            dropSide={dropSide}
            forceDropAfter={appendsToEnd && i === tasks.length - 1}
            muted={
              selectedPersonaId !== null &&
              !task.personaIds.includes(selectedPersonaId)
            }
          />
        ))}
      </SortableContext>
    </div>
  )
}

export default TaskCell
