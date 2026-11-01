'use client'

import { PokerRoundDto } from '@/src/services/wayd-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useUpdatePokerRoundDescriptionMutation } from '@/src/store/features/planning/poker-sessions-api'
import { Input, Typography } from 'antd'
import { FC } from 'react'
import styles from './poker-session.module.css'
import { useRoundFieldAutosave } from './use-round-field-autosave'

const { Paragraph } = Typography

export interface RoundDescriptionProps {
  round: PokerRoundDto
  sessionId: string
  sessionKey: number
  canManage: boolean
}

const MAX_LENGTH = 2048

const RoundDescription: FC<RoundDescriptionProps> = ({
  round,
  sessionId,
  sessionKey,
  canManage,
}) => {
  const messageApi = useMessage()
  const [updateDescription] = useUpdatePokerRoundDescriptionMutation()

  const saveDescription = async (roundId: string, description: string) => {
    try {
      const response = await updateDescription({
        sessionId,
        roundId,
        sessionKey,
        request: { description: description || undefined },
      })
      if (response.error) throw response.error
    } catch {
      messageApi.error('Failed to update round description.')
    }
  }

  const field = useRoundFieldAutosave({
    roundId: round.id,
    serverValue: round.description,
    save: saveDescription,
  })

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
      value={field.value}
      onChange={(e) => field.onChange(e.target.value)}
      onBlur={field.onBlur}
      placeholder="Add a description (optional)..."
      maxLength={MAX_LENGTH}
      autoSize={{ minRows: 2, maxRows: 8 }}
      aria-label="Round description"
      className={styles.roundDescriptionInput}
    />
  )
}

export default RoundDescription
