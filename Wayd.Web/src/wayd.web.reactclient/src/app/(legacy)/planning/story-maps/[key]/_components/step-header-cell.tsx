'use client'

import { DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import { Button, Popconfirm } from 'antd'
import { CSSProperties, FC } from 'react'
import { BoardActions } from './board-actions'
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
  /**
   * The lit seam, decided by the board rather than by each cell's own `isOver`.
   *
   * "After this step" and "before the next" are the same gap and resolve to the same index, so a
   * cell lighting its own trailing edge gave one landing position two appearances that swapped as
   * the pointer crossed a midpoint. The board picks one cell per seam instead: the leading edge of
   * whichever step follows it, falling back to a trailing edge for a goal's last step, which has no
   * following step to borrow an edge from.
   */
  showsDropBefore: boolean
  showsDropAfter: boolean
}

/** A step's header cell — one column track wide, above its own task cells. */
const StepHeaderCell: FC<StepHeaderCellProps> = ({
  placement,
  selectedPersonaId,
  actions,
  isLastColumn,
  showsDropBefore,
  showsDropAfter,
}) => {
  const { step, column } = placement

  const {
    attributes,
    listeners,
    setNodeRef,
    style: sortableStyle,
    dragClassName,
  } = useBoardSortable(step.id, !actions.canUpdate)

  const style: CSSProperties = {
    gridRow: STEP_ROW,
    gridColumn: column,
    ...sortableStyle,
  }

  const muted =
    selectedPersonaId !== null && !step.personaIds.includes(selectedPersonaId)

  // Spell out exactly what a delete takes with it; nothing extra to warn about when the step is
  // empty.
  const taskCount = step.tasks.length
  const deleteDescription =
    taskCount === 0
      ? undefined
      : `This will also delete its ${taskCount} ${taskCount === 1 ? 'task' : 'tasks'}.`

  return (
    <div
      ref={setNodeRef}
      data-tour="step-cell"
      // Steps read left-to-right, so the insertion line is a vertical rule on one of the side edges.
      className={`${styles.stepCell} ${dragClassName} ${muted ? styles.muted : ''} ${
        isLastColumn ? styles.lastColumn : ''
      } ${
        showsDropBefore
          ? styles.stepCellDropBefore
          : showsDropAfter
            ? styles.stepCellDropAfter
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
              description={deleteDescription}
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
