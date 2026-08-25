'use client'

import { StoryMapTaskDto } from '@/src/services/wayd-api'
import { DeleteOutlined } from '@ant-design/icons'
import { Button, Popconfirm } from 'antd'
import { FC, MouseEvent } from 'react'
import { BoardActions } from './board-actions'
import { DropSide } from './board-drag'
import InlineEditText from './inline-edit-text'
import PersonaToggleDots from './persona-toggle-dots'
import { useBoardSortable } from './use-board-sortable'
import styles from '@/src/app/(legacy)/planning/story-maps/_components/story-map.module.css'

export interface TaskCardProps {
  task: StoryMapTaskDto
  muted: boolean
  actions: BoardActions
  /** Which edge of the hovered node a drop lands on, from the board's pointer tracking. */
  dropSide: DropSide
  /**
   * Draw the insertion line below this card even though the pointer is over the cell rather than the
   * card — set on the last card when the drop would append to the end of the cell.
   */
  forceDropAfter?: boolean
  /** This is the task the drawer is showing. */
  isSelected: boolean
}

const TaskCard: FC<TaskCardProps> = ({
  task,
  muted,
  actions,
  dropSide,
  forceDropAfter = false,
  isSelected,
}) => {
  const {
    attributes,
    listeners,
    setNodeRef,
    style,
    dragClassName,
    isDragging,
    isDropTarget,
    dropsAfter,
  } = useBoardSortable(task.id, !actions.canUpdate, { dropSide })

  const showLine = isDropTarget || forceDropAfter
  const lineBelow = forceDropAfter || dropsAfter

  // Opening the drawer is the card's own click, so it must not fire for the controls inside it. The
  // inline title, persona dots, and delete button all sit in the card's subtree, and only the dots
  // stop propagation today — checking the target keeps the rule in one place rather than adding a
  // stopPropagation to every control that might ever be added here.
  //
  // A drag also ends in a click event on the card. dnd-kit's 4px activation distance means a real
  // click never sets isDragging, so it is a reliable way to tell the two apart.
  const handleClick = (e: MouseEvent<HTMLDivElement>) => {
    if (isDragging) return
    if ((e.target as HTMLElement).closest('button, input, textarea, a')) return
    // Clicking the open task again closes the panel, so the card is both the way in and the way out.
    actions.onSelectTask(isSelected ? null : task.id)
  }

  return (
    <div
      ref={setNodeRef}
      style={style}
      data-tour="task-card"
      className={`${styles.taskCard} ${styles.clickable} ${dragClassName} ${
        muted ? styles.muted : ''
      } ${isSelected ? styles.taskCardSelected : ''} ${
        showLine
          ? lineBelow
            ? styles.taskCardDropBelow
            : styles.taskCardDropAbove
          : ''
      }`}
      onClick={handleClick}
      {...attributes}
      {...listeners}
    >
      <InlineEditText
        value={task.title}
        onSave={(title) => actions.onRenameTask(task.id, title)}
        disabled={!actions.canUpdate}
        autoEdit={actions.autoEditId === task.id}
        onEditEnd={actions.onAutoEditEnd}
        ariaLabel="Rename task"
        className={styles.taskTitle}
        display={(v) => <span className={styles.taskTitle}>{v}</span>}
      />

      {/* Footer: persona toggles on the left, hover-revealed delete on the right. */}
      <div className={styles.cellFooter}>
        <PersonaToggleDots
          personas={actions.personas}
          linkedPersonaIds={task.personaIds}
          disabled={!actions.canUpdate}
          onToggle={(personaId) =>
            actions.onToggleTaskPersona(task.id, personaId)
          }
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
