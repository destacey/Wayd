'use client'

import {
  StoryMapStepDto,
  StoryMapSwimLaneDto,
  StoryMapTaskDto,
} from '@/src/services/wayd-api'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { Button, Popconfirm } from 'antd'
import { FC } from 'react'
import { BoardActions } from './board-actions'
import InlineEditText from './inline-edit-text'
import TaskCard from './task-card'
import styles from '../../_components/story-map.module.css'

export interface StepColumnProps {
  step: StoryMapStepDto
  swimLanes: StoryMapSwimLaneDto[]
  personaColors: Map<string, string>
  selectedPersonaId: string | null
  actions: BoardActions
}

const StepColumn: FC<StepColumnProps> = ({
  step,
  swimLanes,
  personaColors,
  selectedPersonaId,
  actions,
}) => {
  const orderedSwimLanes = [...swimLanes].sort((a, b) => a.order - b.order)
  const tasksBySwimLane = new Map<string, StoryMapTaskDto[]>()
  for (const task of step.tasks) {
    const list = tasksBySwimLane.get(task.swimLaneId) ?? []
    list.push(task)
    tasksBySwimLane.set(task.swimLaneId, list)
  }

  const stepMuted =
    selectedPersonaId !== null && !step.personaIds.includes(selectedPersonaId)

  return (
    <div className={styles.stepColumn}>
      <div className={`${styles.stepHeader} ${stepMuted ? styles.muted : ''}`}>
        <InlineEditText
          value={step.name}
          onSave={(name) => actions.onRenameStep(step.id, name)}
          disabled={!actions.canUpdate}
          autoEdit={actions.autoEditId === step.id}
          ariaLabel="Rename step"
          className={styles.stepName}
          display={(v) => <span className={styles.stepName}>{v}</span>}
        />
        {actions.canUpdate && (
          <div className={styles.headerActions}>
            <Button
              size="small"
              type="text"
              icon={<PlusOutlined />}
              aria-label="Add task"
              onClick={() => actions.onAddTask(step.id)}
            />
            <Popconfirm
              title="Delete this step?"
              description="Its tasks will be deleted too."
              okText="Delete"
              okButtonProps={{ danger: true }}
              onConfirm={() => actions.onDeleteStep(step.id)}
            >
              <Button
                size="small"
                type="text"
                icon={<DeleteOutlined />}
                aria-label="Delete step"
              />
            </Popconfirm>
          </div>
        )}
      </div>
      {orderedSwimLanes.map((lane) => {
        const swimLaneTasks = (tasksBySwimLane.get(lane.id) ?? []).sort(
          (a, b) => a.order - b.order,
        )
        return (
          <div key={lane.id} className={styles.swimLaneSection}>
            {swimLaneTasks.map((task) => (
              <TaskCard
                key={task.id}
                task={task}
                personaColors={personaColors}
                actions={actions}
                muted={
                  selectedPersonaId !== null &&
                  !task.personaIds.includes(selectedPersonaId)
                }
              />
            ))}
          </div>
        )
      })}
    </div>
  )
}

export default StepColumn
