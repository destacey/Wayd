'use client'

import { PokerRoundDto } from '@/src/services/wayd-api'
import { useMessage } from '@/src/components/contexts/messaging'
import { useUpdatePokerRoundLabelMutation } from '@/src/store/features/planning/poker-sessions-api'
import EntityLink from '@/src/components/common/entity-link'
import RecordKey from '@/src/components/common/record-key'
import { Flex, Input, Typography } from 'antd'
import Link from 'next/link'
import { FC, useId } from 'react'
import styles from './poker-session.module.css'
import RoundDescription from './round-description'
import { subjectHref } from './round-title'
import { useRoundFieldAutosave } from './use-round-field-autosave'

const { Paragraph, Text } = Typography

export interface RoundLabelHeaderProps {
  round: PokerRoundDto
  sessionId: string
  sessionKey: number
  canManage: boolean
  /**
   * Whether the record a round estimates links to its own page. For those who
   * work the area hosting the session; anyone else reads it as text, since the
   * record may not be theirs to open.
   */
  linkSubject?: boolean
}

const RoundLabelHeader: FC<RoundLabelHeaderProps> = ({
  round,
  sessionId,
  sessionKey,
  canManage,
  linkSubject,
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

  // A round about a record takes its label and description from the record, as
  // they read when it was added, so neither is edited here.
  if (round.subject) {
    // Opens beside the session, so following it does not take a facilitator out of the round.
    const href = linkSubject ? subjectHref(round.subject) : undefined
    return (
      <div className={styles.roundLabelContainer}>
        {/* The key as its own chip before the title, as a record's header lays them out. */}
        <Flex align="center" gap={10} className={styles.roundLabelHeader}>
          {href ? (
            <Link
              href={href}
              target="_blank"
              aria-label={`Open ${round.subject.displayKey}`}
            >
              <RecordKey value={round.subject.displayKey} />
            </Link>
          ) : (
            <RecordKey value={round.subject.displayKey} />
          )}
          <span style={{ minWidth: 0 }}>
            {href ? (
              <EntityLink href={href} target="_blank">
                {round.label}
              </EntityLink>
            ) : (
              round.label
            )}
          </span>
        </Flex>
        {round.description && (
          <Paragraph
            className={styles.roundDescriptionText}
            ellipsis={{ rows: 4, expandable: 'collapsible' }}
          >
            {round.description}
          </Paragraph>
        )}
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
        placeholder="What are you estimating?"
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
