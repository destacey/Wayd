'use client'

import { StoryMapTaskDto } from '@/src/services/wayd-api'
import { PlusOutlined } from '@ant-design/icons'
import { useDroppable } from '@dnd-kit/core'
import { SortableContext, verticalListSortingStrategy } from '@dnd-kit/sortable'
import { CSSProperties, FC } from 'react'
import { BoardActions } from './board-actions'
import { DropSide } from './board-drag'
import TaskCard from './task-card'
import styles from '@/src/app/planning/story-maps/_components/story-map.module.css'

export interface TaskCellProps {
  tasks: StoryMapTaskDto[]
  /** Identifies this (step × swim lane) cell as a drop target. */
  cellId: string
  /** The cell's own coordinates, so a task added here lands in this lane rather than the default. */
  stepId: string
  swimLaneId: string
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
  /** A task from another cell would land here, so this cell is the destination. */
  isReceiving: boolean
}

/**
 * One cell of the task grid: a step column crossed with a swim-lane row. Renders even when empty, so
 * the grid keeps its borders and the cell stays a valid drop target.
 */
const TaskCell: FC<TaskCellProps> = ({
  tasks,
  cellId,
  stepId,
  swimLaneId,
  column,
  row,
  selectedPersonaId,
  actions,
  isLastColumn,
  dropSide,
  isReceiving,
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
      // The receiving outline marks the destination cell; .taskCellOver is the append-here glow an
      // empty cell gets under the pointer. Both can apply at once — an empty cell in another step
      // is both — and .taskCellOver's own outline wins, which is the louder and more specific of
      // the two signals.
      className={`${styles.taskCell} ${isLastColumn ? styles.lastColumn : ''} ${
        isReceiving ? styles.taskCellReceiving : ''
      } ${isOver && !appendsToEnd ? styles.taskCellOver : ''}`}
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
            isSelected={actions.selectedTaskId === task.id}
            muted={
              selectedPersonaId !== null &&
              !task.personaIds.includes(selectedPersonaId)
            }
          />
        ))}
      </SortableContext>

      {/* Hidden mid-drag: the cell is a drop target then, and a button inside it would offer a
          second meaning for the same space. */}
      {actions.canUpdate && !isOver && (
        <button
          type="button"
          className={styles.ghostTask}
          aria-label="Add task"
          onClick={() => actions.onAddTask(stepId, swimLaneId)}
        >
          <PlusOutlined />
          Task
        </button>
      )}
    </div>
  )
}

export default TaskCell
