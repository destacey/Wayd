'use client'

import { PokerRoundDto } from '@/src/services/wayd-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useUpdatePokerRoundLabelMutation } from '@/src/store/features/planning/poker-sessions-api'
import { Input, Typography } from 'antd'
import { FC, useEffect, useRef, useState } from 'react'
import styles from './poker-session.module.css'

const { Text } = Typography

export interface RoundLabelHeaderProps {
  round: PokerRoundDto
  sessionId: string
  sessionKey: number
  canManage: boolean
}

const DEBOUNCE_MS = 600

const RoundLabelHeader: FC<RoundLabelHeaderProps> = ({
  round,
  sessionId,
  sessionKey,
  canManage,
}) => {
  const messageApi = useMessage()
  const [updateLabel] = useUpdatePokerRoundLabelMutation()
  const [localValue, setLocalValue] = useState(round.label ?? '')
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const lastSavedRef = useRef(round.label ?? '')

  // Sync from server when the round changes (different round selected)
  useEffect(() => {
    setLocalValue(round.label ?? '')
    lastSavedRef.current = round.label ?? ''
  }, [round.id, round.label])

  const saveLabel = async (value: string) => {
    const trimmed = value.trim()
    if (trimmed === lastSavedRef.current) return
    lastSavedRef.current = trimmed
    try {
      const response = await updateLabel({
        sessionId,
        roundId: round.id,
        sessionKey,
        request: { label: trimmed || undefined },
      })
      if (response.error) throw response.error
    } catch {
      messageApi.error('Failed to update round label.')
    }
  }

  // Cleanup pending debounce on unmount
  useEffect(() => {
    return () => {
      if (debounceRef.current) {
        clearTimeout(debounceRef.current)
      }
    }
  }, [])

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = e.target.value
    setLocalValue(value)

    if (debounceRef.current) {
      clearTimeout(debounceRef.current)
    }
    debounceRef.current = setTimeout(() => {
      saveLabel(value)
    }, DEBOUNCE_MS)
  }

  const handleBlur = () => {
    if (debounceRef.current) {
      clearTimeout(debounceRef.current)
      debounceRef.current = null
    }
    saveLabel(localValue)
  }

  const isEditable = canManage

  if (!isEditable) {
    return (
      <div className={styles.roundLabelContainer}>
        <div className={styles.roundLabelHeader}>
          {round.label || 'Untitled round'}
        </div>
      </div>
    )
  }

  return (
    <div className={styles.roundLabelContainer}>
      <Text type="secondary" className={styles.roundLabelTitle}>
        What are you estimating?
      </Text>
      <Input
        value={localValue}
        onChange={handleChange}
        onBlur={handleBlur}
        placeholder="Type a work item ID or description..."
        maxLength={512}
        size="large"
        className={styles.roundLabelInput}
      />
    </div>
  )
}

export default RoundLabelHeader
