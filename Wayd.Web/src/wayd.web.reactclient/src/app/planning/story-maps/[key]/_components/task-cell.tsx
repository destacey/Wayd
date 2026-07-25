'use client'

import { StoryMapTaskDto } from '@/src/services/wayd-api'
import { CSSProperties, FC } from 'react'
import { BoardActions } from './board-actions'
import TaskCard from './task-card'
import styles from '../../_components/story-map.module.css'

export interface TaskCellProps {
  tasks: StoryMapTaskDto[]
  /** 1-based grid column of the owning step. */
  column: number
  /** 1-based grid row of the owning swim lane. */
  row: number
  selectedPersonaId: string | null
  actions: BoardActions
  /** Cells in the right-most column drop the border that would double against the grid's own. */
  isLastColumn: boolean
}

/**
 * One cell of the task grid: the intersection of a step column and a swim-lane row. Tasks stack
 * vertically inside it. The cell renders even when empty so the grid keeps its borders and stays a
 * valid drop target once drag-and-drop lands.
 */
const TaskCell: FC<TaskCellProps> = ({
  tasks,
  column,
  row,
  selectedPersonaId,
  actions,
  isLastColumn,
}) => {
  const style: CSSProperties = { gridRow: row, gridColumn: column }

  return (
    <div
      className={`${styles.taskCell} ${isLastColumn ? styles.lastColumn : ''}`}
      style={style}
    >
      {tasks.map((task) => (
        <TaskCard
          key={task.id}
          task={task}
          actions={actions}
          muted={
            selectedPersonaId !== null &&
            !task.personaIds.includes(selectedPersonaId)
          }
        />
      ))}
    </div>
  )
}

export default TaskCell
