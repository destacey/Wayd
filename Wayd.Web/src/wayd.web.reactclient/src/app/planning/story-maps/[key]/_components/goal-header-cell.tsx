'use client'

import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { Button, Popconfirm } from 'antd'
import { CSSProperties, FC } from 'react'
import { BoardActions } from './board-actions'
import { DropSide } from './board-drag'
import { GoalPlacement, GOAL_ROW } from './board-layout'
import InlineEditText from './inline-edit-text'
import { useBoardSortable } from './use-board-sortable'
import styles from '../../_components/story-map.module.css'

export interface GoalHeaderCellProps {
  placement: GoalPlacement
  selectedPersonaId: string | null
  actions: BoardActions
  onAddStep: (goalId: string) => void
  /** Cells in the right-most column drop the border that would double against the grid's own. */
  isLastColumn: boolean
  /** Which edge of the hovered node a drop lands on, from the board's pointer tracking. */
  dropSide: DropSide
}

/**
 * A goal's header cell on the goals row. It spans the column tracks of all its steps, so the goal
 * reads as a banner sitting directly above the steps it owns.
 */
const GoalHeaderCell: FC<GoalHeaderCellProps> = ({
  placement,
  selectedPersonaId,
  actions,
  onAddStep,
  isLastColumn,
  dropSide,
}) => {
  const { goal, columnStart, columnSpan } = placement

  const {
    attributes,
    listeners,
    setNodeRef,
    style: sortableStyle,
    isDropTarget,
    dropsAfter,
  } = useBoardSortable(goal.id, !actions.canUpdate, { dropSide })

  const style: CSSProperties = {
    gridRow: GOAL_ROW,
    gridColumn: `${columnStart} / span ${columnSpan}`,
    ...sortableStyle,
  }

  const muted =
    selectedPersonaId !== null && !goal.personaIds.includes(selectedPersonaId)

  return (
    <div
      ref={setNodeRef}
      // Goals read left-to-right, so their insertion line is vertical too.
      className={`${styles.goalCell} ${muted ? styles.muted : ''} ${
        isLastColumn ? styles.lastColumn : ''
      } ${
        isDropTarget
          ? dropsAfter
            ? styles.stepCellDropAfter
            : styles.stepCellDropBefore
          : ''
      }`}
      style={style}
      {...attributes}
      {...listeners}
      aria-label={actions.canUpdate ? `Reorder ${goal.name}` : undefined}
    >
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
  )
}

export default GoalHeaderCell
