'use client'

import { Flex } from 'antd'
import { FC } from 'react'
import styles from './poker-session.module.css'

export interface EstimationCardDeckProps {
  values: string[]
  selectedValue?: string
  onSelect: (value: string) => void
  disabled?: boolean
}

const EstimationCardDeck: FC<EstimationCardDeckProps> = ({
  values,
  selectedValue,
  onSelect,
  disabled = false,
}) => {
  return (
    <Flex wrap gap={8} justify="center">
      {values.map((v) => {
        const isSelected = selectedValue === v
        const className = `${styles.estimationCard}${isSelected ? ` ${styles.estimationCardSelected}` : ''}`

        return (
          <button
            key={v}
            type="button"
            className={className}
            onClick={() => onSelect(v)}
            disabled={disabled}
          >
            {v}
          </button>
        )
      })}
    </Flex>
  )
}

export default EstimationCardDeck
