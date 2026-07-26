'use client'

import { StoryMapTaskDto } from '@/src/services/wayd-api'
import { DeleteOutlined } from '@ant-design/icons'
import { Button, Popconfirm } from 'antd'
import { FC } from 'react'
import { BoardActions } from './board-actions'
import { DropSide } from './board-drag'
import InlineEditText from './inline-edit-text'
import PersonaToggleDots from './persona-toggle-dots'
import { useBoardSortable } from './use-board-sortable'
import styles from '../../_components/story-map.module.css'

export interface TaskCardProps {
  task: StoryMapTaskDto
  muted: boolean
  actions: BoardActions
  /** Which edge of the hovered node a drop lands on, from the board's pointer tracking. */
  dropSide: DropSide
}

const TaskCard: FC<TaskCardProps> = ({ task, muted, actions, dropSide }) => {
  const { attributes, listeners, setNodeRef, style, isDropTarget, dropsAfter } =
    useBoardSortable(task.id, !actions.canUpdate, { dropSide })

  return (
  <div
    ref={setNodeRef}
    style={style}
    // The insertion line marks the seam the task will land on. Which edge depends on direction: a
    // downward drag within the cell lands below this card, everything else lands above it. Drawing
    // it on the wrong edge is the difference between the indicator agreeing with the drop and
    // silently contradicting it.
    className={`${styles.taskCard} ${muted ? styles.muted : ''} ${
      isDropTarget
        ? dropsAfter
          ? styles.taskCardDropBelow
          : styles.taskCardDropAbove
        : ''
    }`}
    {...attributes}
    {...listeners}
    aria-label={actions.canUpdate ? `Reorder ${task.title}` : undefined}
  >
    <InlineEditText
      value={task.title}
      onSave={(title) => actions.onRenameTask(task, title)}
      disabled={!actions.canUpdate}
      autoEdit={actions.autoEditId === task.id}
      ariaLabel="Rename task"
      maxLength={256}
      className={styles.taskTitle}
      display={(v) => <span className={styles.taskTitle}>{v}</span>}
    />

    {/* Footer: persona toggles on the left, hover-revealed delete on the right. */}
    <div className={styles.cellFooter}>
      <PersonaToggleDots
        personas={actions.personas}
        linkedPersonaIds={task.personaIds}
        disabled={!actions.canUpdate}
        onToggle={(personaId) => actions.onToggleTaskPersona(task.id, personaId)}
      />
      <div className={styles.footerTrailing}>
        {task.checklistTotalCount > 0 && (
          <span className={styles.checklistCount}>
            {task.checklistCompletedCount}/{task.checklistTotalCount}
          </span>
        )}
        {actions.canUpdate && (
          <div className={styles.footerActions}>
            <Popconfirm
              title="Delete this task?"
              okText="Delete"
              okButtonProps={{ danger: true }}
              onConfirm={() => actions.onDeleteTask(task.id)}
            >
              <Button
                size="small"
                type="text"
                icon={<DeleteOutlined />}
                aria-label="Delete task"
              />
            </Popconfirm>
          </div>
        )}
      </div>
    </div>
  </div>
  )
}

export default TaskCard
