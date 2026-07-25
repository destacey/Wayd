'use client'

import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { Button, Popconfirm } from 'antd'
import { CSSProperties, FC } from 'react'
import { BoardActions } from './board-actions'
import { StepPlacement, STEP_ROW } from './board-layout'
import InlineEditText from './inline-edit-text'
import PersonaToggleDots from './persona-toggle-dots'
import styles from '../../_components/story-map.module.css'

export interface StepHeaderCellProps {
  placement: StepPlacement
  selectedPersonaId: string | null
  actions: BoardActions
  /** Cells in the right-most column drop the border that would double against the grid's own. */
  isLastColumn: boolean
}

/**
 * A step's header cell on the steps row — one column track wide, sitting under its goal's banner
 * and above its own task cells.
 */
const StepHeaderCell: FC<StepHeaderCellProps> = ({
  placement,
  selectedPersonaId,
  actions,
  isLastColumn,
}) => {
  const { step, column } = placement

  const style: CSSProperties = { gridRow: STEP_ROW, gridColumn: column }

  const muted =
    selectedPersonaId !== null && !step.personaIds.includes(selectedPersonaId)

  return (
    <div
      className={`${styles.stepCell} ${muted ? styles.muted : ''} ${
        isLastColumn ? styles.lastColumn : ''
      }`}
      style={style}
    >
      <InlineEditText
        value={step.name}
        onSave={(name) => actions.onRenameStep(step.id, name)}
        disabled={!actions.canUpdate}
        autoEdit={actions.autoEditId === step.id}
        ariaLabel="Rename step"
        className={styles.stepName}
        display={(v) => <span className={styles.stepName}>{v}</span>}
      />

      {/* Footer: persona toggles on the left, hover-revealed add/delete on the right. */}
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
