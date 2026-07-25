'use client'

import {
  StoryMapGoalDto,
  StoryMapSwimLaneDto,
} from '@/src/services/wayd-api'
import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { Button, Popconfirm } from 'antd'
import { FC } from 'react'
import { BoardActions } from './board-actions'
import InlineEditText from './inline-edit-text'
import StepColumn from './step-column'
import styles from '../../_components/story-map.module.css'

export interface GoalColumnProps {
  goal: StoryMapGoalDto
  swimLanes: StoryMapSwimLaneDto[]
  personaColors: Map<string, string>
  selectedPersonaId: string | null
  actions: BoardActions
  onAddStep: (goalId: string) => void
}

const GoalColumn: FC<GoalColumnProps> = ({
  goal,
  swimLanes,
  personaColors,
  selectedPersonaId,
  actions,
  onAddStep,
}) => {
  const orderedSteps = [...goal.steps].sort((a, b) => a.order - b.order)

  return (
    <div className={styles.goalColumn}>
      <div className={styles.goalHeader}>
        <InlineEditText
          value={goal.name}
          onSave={(name) => actions.onRenameGoal(goal.id, name)}
          disabled={!actions.canUpdate}
          autoEdit={actions.autoEditId === goal.id}
          ariaLabel="Rename goal"
          className={styles.goalName}
          display={(v) => <span className={styles.goalName}>{v}</span>}
        />
        {actions.canUpdate && (
          <div className={styles.headerActions}>
            <Button
              size="small"
              type="text"
              icon={<PlusOutlined />}
              aria-label="Add step"
              onClick={() => onAddStep(goal.id)}
            />
            <Popconfirm
              title="Delete this goal?"
              description="Its steps and their tasks will be deleted too."
              okText="Delete"
              okButtonProps={{ danger: true }}
              onConfirm={() => actions.onDeleteGoal(goal.id)}
            >
              <Button
                size="small"
                type="text"
                icon={<DeleteOutlined />}
                aria-label="Delete goal"
              />
            </Popconfirm>
          </div>
        )}
      </div>
      <div className={styles.stepRow}>
        {orderedSteps.map((step) => (
          <StepColumn
            key={step.id}
            step={step}
            swimLanes={swimLanes}
            personaColors={personaColors}
            selectedPersonaId={selectedPersonaId}
            actions={actions}
          />
        ))}
      </div>
    </div>
  )
}

export default GoalColumn
