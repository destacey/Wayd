'use client'

import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { Button, Popconfirm } from 'antd'
import { CSSProperties, FC } from 'react'
import { BoardActions } from './board-actions'
import { DropSide } from './board-drag'
import { StepPlacement, STEP_ROW } from './board-layout'
import InlineEditText from './inline-edit-text'
import PersonaToggleDots from './persona-toggle-dots'
import { useBoardSortable } from './use-board-sortable'
import styles from '../../_components/story-map.module.css'

export interface StepHeaderCellProps {
  placement: StepPlacement
  selectedPersonaId: string | null
  actions: BoardActions
  /** Cells in the right-most column drop the border that would double against the grid's own. */
  isLastColumn: boolean
  /** Which edge of the hovered node a drop lands on, from the board's pointer tracking. */
  dropSide: DropSide
}

/** A step's header cell — one column track wide, above its own task cells. */
const StepHeaderCell: FC<StepHeaderCellProps> = ({
  placement,
  selectedPersonaId,
  actions,
  isLastColumn,
  dropSide,
}) => {
  const { step, column } = placement

  const {
    attributes,
    listeners,
    setNodeRef,
    style: sortableStyle,
    dragClassName,
    isDropTarget,
    dropsAfter,
  } = useBoardSortable(step.id, !actions.canUpdate, { dropSide })

  const style: CSSProperties = {
    gridRow: STEP_ROW,
    gridColumn: column,
    ...sortableStyle,
  }

  const muted =
    selectedPersonaId !== null && !step.personaIds.includes(selectedPersonaId)

  return (
    <div
      ref={setNodeRef}
      data-tour="step-cell"
      // Steps read left-to-right, so the insertion line is a vertical rule on one of the side edges.
      className={`${styles.stepCell} ${dragClassName} ${muted ? styles.muted : ''} ${
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
    >
      <InlineEditText
        value={step.name}
        onSave={(name) => actions.onRenameStep(step.id, name)}
        disabled={!actions.canUpdate}
        autoEdit={actions.autoEditId === step.id}
        onEditEnd={actions.onAutoEditEnd}
        ariaLabel="Rename step"
        className={styles.stepName}
        display={(v) => <span className={styles.stepName}>{v}</span>}
      />

      {/* Footer: persona toggles on the left, add/delete on the right. */}
      <div className={styles.cellFooter}>
        <PersonaToggleDots
          personas={actions.personas}
          linkedPersonaIds={step.personaIds}
          disabled={!actions.canUpdate}
          onToggle={(personaId) =>
            actions.onToggleStepPersona(step.id, personaId)
          }
        />
        {actions.canUpdate && (
          <div className={styles.footerActions}>
            <Button
              size="small"
              type="text"
              icon={<PlusOutlined />}
              aria-label="Add task"
              data-tour="add-task"
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
    </div>
  )
}

export default StepHeaderCell
