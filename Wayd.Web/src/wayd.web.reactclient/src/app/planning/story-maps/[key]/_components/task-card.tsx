'use client'

import { StoryMapTaskDto } from '@/src/services/wayd-api'
import { DeleteOutlined } from '@ant-design/icons'
import { Button, Popconfirm } from 'antd'
import { FC } from 'react'
import { BoardActions } from './board-actions'
import InlineEditText from './inline-edit-text'
import PersonaToggleDots from './persona-toggle-dots'
import styles from '../../_components/story-map.module.css'

export interface TaskCardProps {
  task: StoryMapTaskDto
  muted: boolean
  actions: BoardActions
}

const TaskCard: FC<TaskCardProps> = ({ task, muted, actions }) => (
  <div className={`${styles.taskCard} ${muted ? styles.muted : ''}`}>
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

export default TaskCard
