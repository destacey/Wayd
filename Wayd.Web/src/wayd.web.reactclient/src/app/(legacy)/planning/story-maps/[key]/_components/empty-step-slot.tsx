'use client'

import { useDroppable } from '@dnd-kit/core'
import { PlusOutlined } from '@ant-design/icons'
import { CSSProperties, FC } from 'react'
import { emptyStepSlotId } from './board-drag'
import { STEP_ROW } from './board-layout'
import styles from '../../_components/story-map.module.css'

export interface EmptyStepSlotProps {
  goalId: string
  /** 1-based grid column of the step-less goal's placeholder track. */
  column: number
  canUpdate: boolean
  isLastColumn: boolean
  onAddStep: (goalId: string) => void
}

/**
 * The empty slot in the steps row beneath a step-less goal. Fills the placeholder column so the grid
 * has no hole, is the drop target for moving a step into that goal, and for editors is a ghost step
 * that adds the goal's first one when clicked.
 *
 * A <button> only for editors — a viewer who cannot add gets no focusable element promising an
 * action that would fail.
 */
const EmptyStepSlot: FC<EmptyStepSlotProps> = ({
  goalId,
  column,
  canUpdate,
  isLastColumn,
  onAddStep,
}) => {
  const { setNodeRef, isOver } = useDroppable({
    id: emptyStepSlotId(goalId),
    disabled: !canUpdate,
  })

  const style: CSSProperties = { gridRow: STEP_ROW, gridColumn: column }

  const className = `${styles.stepCell} ${styles.emptyStepSlot} ${
    isLastColumn ? styles.lastColumn : ''
  } ${isOver ? styles.stepCellOver : ''}`

  if (!canUpdate) {
    return <div ref={setNodeRef} className={className} style={style} />
  }

  return (
    <button
      ref={setNodeRef}
      type="button"
      className={`${className} ${styles.emptyStepSlotButton}`}
      style={style}
      aria-label="Add the first step"
      onClick={() => onAddStep(goalId)}
    >
      {/* The drop highlight is the message while a step is held over the slot. */}
      {!isOver && (
        <span className={styles.emptyStepSlotHint}>
          <PlusOutlined />
          Add step
        </span>
      )}
    </button>
  )
}

export default EmptyStepSlot
