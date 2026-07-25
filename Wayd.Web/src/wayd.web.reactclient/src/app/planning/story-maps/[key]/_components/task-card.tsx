'use client'

import { StoryMapTaskDto } from '@/src/services/wayd-api'
import { DeleteOutlined } from '@ant-design/icons'
import { Button, Popconfirm } from 'antd'
import { FC } from 'react'
import { BoardActions } from './board-actions'
import InlineEditText from './inline-edit-text'
import styles from '../../_components/story-map.module.css'

export interface TaskCardProps {
  task: StoryMapTaskDto
  personaColors: Map<string, string>
  muted: boolean
  actions: BoardActions
}

const TaskCard: FC<TaskCardProps> = ({
  task,
  personaColors,
  muted,
  actions,
}) => (
  <div className={`${styles.taskCard} ${muted ? styles.muted : ''}`}>
    <div className={styles.taskCardHeader}>
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
      {actions.canUpdate && (
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
            className={styles.cardDeleteButton}
          />
        </Popconfirm>
      )}
    </div>
    <div className={styles.taskMeta}>
      {task.personaIds.length > 0 && (
        <span className={styles.personaDots}>
          {task.personaIds.map((personaId) => (
            <span
              key={personaId}
              className={styles.personaDot}
              style={{
                backgroundColor:
                  personaColors.get(personaId) ?? 'var(--sm-muted)',
              }}
            />
          ))}
        </span>
      )}
      {task.checklistTotalCount > 0 && (
        <span className={styles.checklistCount}>
          {task.checklistCompletedCount}/{task.checklistTotalCount}
        </span>
      )}
    </div>
  </div>
)

export default TaskCard
