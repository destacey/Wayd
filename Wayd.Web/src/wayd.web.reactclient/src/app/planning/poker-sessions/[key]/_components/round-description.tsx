'use client'

import { PokerRoundDto } from '@/src/services/wayd-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useUpdatePokerRoundDescriptionMutation } from '@/src/store/features/planning/poker-sessions-api'
import { Input, Typography } from 'antd'
import { FC, useEffect, useRef, useState } from 'react'
import styles from './poker-session.module.css'

const { Paragraph } = Typography

export interface RoundDescriptionProps {
  round: PokerRoundDto
  sessionId: string
  sessionKey: number
  canManage: boolean
}

const DEBOUNCE_MS = 600
const MAX_LENGTH = 2048

const RoundDescription: FC<RoundDescriptionProps> = ({
  round,
  sessionId,
  sessionKey,
  canManage,
}) => {
  const messageApi = useMessage()
  const [updateDescription] = useUpdatePokerRoundDescriptionMutation()
  const [localValue, setLocalValue] = useState(round.description ?? '')
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null)
  const lastSavedRef = useRef(round.description ?? '')

  // Take the server's description when another round is selected or its description changes.
  const [syncedRound, setSyncedRound] = useState({
    id: round.id,
    description: round.description,
  })
  if (
    syncedRound.id !== round.id ||
    syncedRound.description !== round.description
  ) {
    setSyncedRound({ id: round.id, description: round.description })
    setLocalValue(round.description ?? '')
  }

  useEffect(() => {
    lastSavedRef.current = round.description ?? ''
  }, [round.id, round.description])

  useEffect(() => {
    return () => {
      if (debounceRef.current) {
        clearTimeout(debounceRef.current)
      }
    }
  }, [])

  const saveDescription = async (value: string) => {
    const trimmed = value.trim()
    if (trimmed === lastSavedRef.current) return
    lastSavedRef.current = trimmed
    try {
      const response = await updateDescription({
        sessionId,
        roundId: round.id,
        sessionKey,
        request: { description: trimmed || undefined },
      })
      if (response.error) throw response.error
    } catch {
      messageApi.error('Failed to update round description.')
    }
  }

  const handleChange = (e: React.ChangeEvent<HTMLTextAreaElement>) => {
    const value = e.target.value
    setLocalValue(value)

    if (debounceRef.current) {
      clearTimeout(debounceRef.current)
    }
    debounceRef.current = setTimeout(() => {
      saveDescription(value)
    }, DEBOUNCE_MS)
  }

  const handleBlur = () => {
    if (debounceRef.current) {
      clearTimeout(debounceRef.current)
      debounceRef.current = null
    }
    saveDescription(localValue)
  }

  if (!canManage) {
    if (!round.description) return null
    return (
      <Paragraph
        type="secondary"
        className={styles.roundDescriptionText}
        ellipsis={{ rows: 3, expandable: 'collapsible' }}
      >
        {round.description}
      </Paragraph>
    )
  }

  return (
    <Input.TextArea
      value={localValue}
      onChange={handleChange}
      onBlur={handleBlur}
      placeholder="Add a description (optional)..."
      maxLength={MAX_LENGTH}
      autoSize={{ minRows: 2, maxRows: 8 }}
      aria-label="Round description"
      className={styles.roundDescriptionInput}
    />
  )
}

export default RoundDescription
