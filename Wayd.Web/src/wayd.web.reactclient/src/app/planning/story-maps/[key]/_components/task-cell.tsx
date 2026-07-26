'use client'

import { StoryMapTaskDto } from '@/src/services/wayd-api'
import { useDroppable } from '@dnd-kit/core'
import {
  SortableContext,
  verticalListSortingStrategy,
} from '@dnd-kit/sortable'
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
 * One cell of the task grid: the intersection of a step column and a swim-lane row. Tasks stack
 * vertically inside it. The cell renders even when empty so the grid keeps its borders and stays a
 * valid drop target once drag-and-drop lands.
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
  // The cell itself is a drop target, not just its cards: an empty cell has no sortable items, so
  // without this a task could never be dropped into one. When the pointer is over one of its cards
  // instead, that card owns the drop and draws its own insertion line — dnd-kit reports isOver on
  // the innermost target only, so the two never light up at once.
  const { setNodeRef, isOver } = useDroppable({
    id: cellId,
    disabled: !actions.canUpdate,
  })

  const style: CSSProperties = { gridRow: row, gridColumn: column }

  return (
    <div
      ref={setNodeRef}
      className={`${styles.taskCell} ${isLastColumn ? styles.lastColumn : ''} ${
        isOver ? styles.taskCellOver : ''
      }`}
      style={style}
    >
      <SortableContext
        items={tasks.map((t) => t.id)}
        strategy={verticalListSortingStrategy}
      >
        {tasks.map((task) => (
          <TaskCard
            key={task.id}
            task={task}
            actions={actions}
            dropSide={dropSide}
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
