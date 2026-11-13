'use client'

import { PokerRoundDto } from '@/src/services/wayd-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useUpdatePokerRoundLabelMutation } from '@/src/store/features/planning/poker-sessions-api'
import { Input, Typography } from 'antd'
import { FC, useId } from 'react'
import styles from './poker-session.module.css'
import RoundDescription from './round-description'
import { useRoundFieldAutosave } from './use-round-field-autosave'

const { Paragraph, Text } = Typography

export interface RoundLabelHeaderProps {
  round: PokerRoundDto
  sessionId: string
  sessionKey: number
  canManage: boolean
}

const RoundLabelHeader: FC<RoundLabelHeaderProps> = ({
  round,
  sessionId,
  sessionKey,
  canManage,
}) => {
  const messageApi = useMessage()
  const [updateLabel] = useUpdatePokerRoundLabelMutation()
  const labelTitleId = useId()

  const saveLabel = async (roundId: string, label: string) => {
    try {
      const response = await updateLabel({
        sessionId,
        roundId,
        sessionKey,
        request: { label: label || undefined },
      })
      if (response.error) throw response.error
    } catch {
      messageApi.error('Failed to update round label.')
    }
  }

  const field = useRoundFieldAutosave({
    roundId: round.id,
    serverValue: round.label,
    save: saveLabel,
  })

  const description = (
    <RoundDescription
      round={round}
      sessionId={sessionId}
      sessionKey={sessionKey}
      canManage={canManage}
    />
  )

  // A round about a record reads as the record did when it was added; only the
  // facilitators' own notes stay editable.
  if (round.subject) {
    return (
      <div className={styles.roundLabelContainer}>
        <Text type="secondary" className={styles.roundLabelTitle}>
          {round.subject.displayKey}
        </Text>
        <div className={styles.roundLabelHeader}>{round.subject.title}</div>
        {round.subject.description && (
          <Paragraph
            className={styles.roundDescriptionText}
            ellipsis={{ rows: 4, expandable: 'collapsible' }}
          >
            {round.subject.description}
          </Paragraph>
        )}
        {description}
      </div>
    )
  }

  if (!canManage) {
    return (
      <div className={styles.roundLabelContainer}>
        <div className={styles.roundLabelHeader}>
          {round.label || 'Untitled round'}
        </div>
        {description}
      </div>
    )
  }

  return (
    <div className={styles.roundLabelContainer}>
      <Text
        id={labelTitleId}
        type="secondary"
        className={styles.roundLabelTitle}
      >
        What are you estimating?
      </Text>
      <Input
        value={field.value}
        onChange={(e) => field.onChange(e.target.value)}
        onBlur={field.onBlur}
        placeholder="Type a work item ID or title..."
        maxLength={512}
        size="large"
        aria-labelledby={labelTitleId}
        className={styles.roundLabelInput}
      />
      {description}
    </div>
  )
}

export default RoundLabelHeader
