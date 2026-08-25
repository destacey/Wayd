'use client'

import {
  DeleteOutlined,
  DownOutlined,
  PlusOutlined,
  RightOutlined,
} from '@ant-design/icons'
import { Button, Popconfirm } from 'antd'
import { WaydTooltip } from '@/src/components/common'
import { CSSProperties, FC } from 'react'
import { BoardActions } from './board-actions'
import { DropSide } from './board-drag'
import { GoalPlacement, GOAL_ROW } from './board-layout'
import InlineEditText from './inline-edit-text'
import { useBoardSortable } from './use-board-sortable'
import styles from '@/src/app/(legacy)/planning/story-maps/_components/story-map.module.css'

export interface GoalHeaderCellProps {
  placement: GoalPlacement
  selectedPersonaId: string | null
  actions: BoardActions
  onAddStep: (goalId: string) => void
  /** Cells in the right-most column drop the border that would double against the grid's own. */
  isLastColumn: boolean
  /** Which edge of the hovered node a drop lands on, from the board's pointer tracking. */
  dropSide: DropSide
  onToggleCollapsed: (goalId: string) => void
  /**
   * Grid row line just past the last swim-lane row. A collapsed goal's header spans down to it,
   * since it renders nothing else and its column would otherwise be a hole.
   */
  bottomRow: number
}

/** A goal's header cell, spanning the column tracks of all its steps. */
const GoalHeaderCell: FC<GoalHeaderCellProps> = ({
  placement,
  selectedPersonaId,
  actions,
  onAddStep,
  isLastColumn,
  dropSide,
  onToggleCollapsed,
  bottomRow,
}) => {
  const { goal, columnStart, columnSpan, isCollapsed } = placement

  const {
    attributes,
    listeners,
    setNodeRef,
    style: sortableStyle,
    dragClassName,
    isDropTarget,
    dropsAfter,
  } = useBoardSortable(goal.id, !actions.canUpdate, { dropSide })

  const style: CSSProperties = {
    gridRow: isCollapsed ? `${GOAL_ROW} / ${bottomRow}` : GOAL_ROW,
    gridColumn: `${columnStart} / span ${columnSpan}`,
    ...sortableStyle,
  }

  // Spell out exactly what a delete takes with it. No description at all when the goal is empty —
  // a childless delete has nothing extra to warn about.
  const stepCount = goal.steps.length
  const taskCount = goal.steps.reduce((n, s) => n + s.tasks.length, 0)
  const deleteDescription =
    stepCount === 0
      ? undefined
      : `This will also delete its ${stepCount} ${stepCount === 1 ? 'step' : 'steps'}${
          taskCount > 0
            ? ` and ${taskCount} ${taskCount === 1 ? 'task' : 'tasks'}`
            : ''
        }.`

  // A goal is a container, not something a persona is tagged on directly — nothing in the UI sets a
  // goal's own personaIds. So it stays lit whenever anything beneath it is relevant to the filter;
  // muting on its own (always empty) tags greyed out every goal on the board.
  const muted =
    selectedPersonaId !== null &&
    !goal.personaIds.includes(selectedPersonaId) &&
    !goal.steps.some(
      (step) =>
        step.personaIds.includes(selectedPersonaId) ||
        step.tasks.some((task) => task.personaIds.includes(selectedPersonaId)),
    )

  return (
    <div
      ref={setNodeRef}
      data-tour="goal-cell"
      className={`${styles.goalCell} ${dragClassName} ${muted ? styles.muted : ''} ${
        isCollapsed ? styles.goalCellCollapsed : ''
      } ${isLastColumn ? styles.lastColumn : ''} ${
        isDropTarget
          ? dropsAfter
            ? styles.stepCellDropAfter
            : styles.stepCellDropBefore
          : ''
      }`}
      style={style}
      {...attributes}
      {...listeners}
    >
      <WaydTooltip title={isCollapsed ? 'Expand goal' : 'Collapse goal'}>
        <Button
          size="small"
          type="text"
          icon={isCollapsed ? <RightOutlined /> : <DownOutlined />}
          className={styles.goalCollapseButton}
          aria-expanded={!isCollapsed}
          aria-label={
            isCollapsed ? `Expand ${goal.name}` : `Collapse ${goal.name}`
          }
          onClick={() => onToggleCollapsed(goal.id)}
        />
      </WaydTooltip>

      {/* Not editable while collapsed: a rotated box gives the inline editor nothing to size
          against. Expanding restores it. */}
      {isCollapsed ? (
        <WaydTooltip title={goal.name}>
          <span className={`${styles.goalName} ${styles.goalNameVertical}`}>
            {goal.name}
          </span>
        </WaydTooltip>
      ) : (
        <InlineEditText
          value={goal.name}
          onSave={(name) => actions.onRenameGoal(goal.id, name)}
          disabled={!actions.canUpdate}
          autoEdit={actions.autoEditId === goal.id}
          onEditEnd={actions.onAutoEditEnd}
          ariaLabel="Rename goal"
          className={styles.goalName}
          display={(v) => <span className={styles.goalName}>{v}</span>}
        />
      )}

      {actions.canUpdate && !isCollapsed && (
        <div className={styles.headerActions}>
          <Button
            size="small"
            type="text"
            icon={<PlusOutlined />}
            aria-label="Add step"
            data-tour="add-step"
            onClick={() => onAddStep(goal.id)}
          />
          <Popconfirm
            title="Delete this goal?"
            description={deleteDescription}
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
