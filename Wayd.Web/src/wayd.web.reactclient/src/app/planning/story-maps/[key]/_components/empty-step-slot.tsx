'use client'

import { useDroppable } from '@dnd-kit/core'
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
}

/**
 * The empty slot in the steps row beneath a step-less goal. Fills the placeholder column so the grid
 * has no hole, and is the drop target for moving a step into that goal.
 */
const EmptyStepSlot: FC<EmptyStepSlotProps> = ({
  goalId,
  column,
  canUpdate,
  isLastColumn,
}) => {
  const { setNodeRef, isOver } = useDroppable({
    id: emptyStepSlotId(goalId),
    disabled: !canUpdate,
  })

  const style: CSSProperties = { gridRow: STEP_ROW, gridColumn: column }

  return (
    <div
      ref={setNodeRef}
      className={`${styles.stepCell} ${isLastColumn ? styles.lastColumn : ''} ${
        isOver ? styles.stepCellOver : ''
      }`}
      style={style}
    />
  )
}

export default EmptyStepSlot
