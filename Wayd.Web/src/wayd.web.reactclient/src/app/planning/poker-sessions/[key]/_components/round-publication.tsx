'use client'

import { useMessage } from '@/src/components/contexts/messaging'
import {
  PokerRoundDto,
  PokerRoundPublicationOutcome,
} from '@/src/services/wayd-api'
import { usePublishPokerRoundsMutation } from '@/src/store/features/planning/poker-sessions-api'
import { toApiError } from '@/src/utils'
import { Alert, Button, Flex, Tag, Typography } from 'antd'
import dayjs from 'dayjs'
import { FC } from 'react'

const { Text } = Typography

export interface RoundPublicationProps {
  round: PokerRoundDto
  sessionId: string
  sessionKey: number
  /** Whether the viewer runs the session. Publishing is allowed after it completes, too. */
  canManage: boolean
  /** Whether the consensus is a card such as `?`, which clears the record's estimate. */
  consensusIsCard: boolean
}

/** Whether a round's consensus can be published now. */
export const canPublishRound = (round: PokerRoundDto): boolean =>
  !!round.subject &&
  round.status === 'Accepted' &&
  round.publication?.outcome !== PokerRoundPublicationOutcome.Published

/**
 * Whether an accepted round's estimate has reached the record it estimates,
 * with the action to publish it. Renders nothing for a free-text round, which
 * has no record to publish to.
 */
const RoundPublication: FC<RoundPublicationProps> = ({
  round,
  sessionId,
  sessionKey,
  canManage,
  consensusIsCard,
}) => {
  const messageApi = useMessage()
  const [publish, { isLoading }] = usePublishPokerRoundsMutation()

  if (!round.subject || round.status !== 'Accepted') return null

  const publication = round.publication
  const subject = round.subject.displayKey

  const handlePublish = async () => {
    try {
      const [published] = await publish({
        sessionId,
        sessionKey,
        roundId: round.id,
      }).unwrap()
      if (
        published?.publication?.outcome === PokerRoundPublicationOutcome.Refused
      ) {
        messageApi.warning(
          published.publication.refusalReason ?? 'The estimate was refused.',
        )
      } else {
        messageApi.success(
          consensusIsCard
            ? `Request ${subject}'s estimate was cleared.`
            : `Request ${subject} is estimated at ${round.consensusEstimate}.`,
        )
      }
    } catch (error) {
      messageApi.error(
        toApiError(error).detail ?? 'Failed to publish the estimate.',
      )
    }
  }

  if (publication?.outcome === PokerRoundPublicationOutcome.Published) {
    return (
      <Flex justify="center" align="center" gap={8} wrap>
        <Tag color="success" style={{ margin: 0 }}>
          Published
        </Tag>
        <Text type="secondary" style={{ fontSize: 12 }}>
          {publication.by ? `by ${publication.by.name} ` : ''}on{' '}
          {dayjs(publication.at).format('MMM D, YYYY h:mm A')}
        </Text>
      </Flex>
    )
  }

  const publishButton = canManage && (
    <Button
      type={publication ? 'default' : 'primary'}
      onClick={handlePublish}
      loading={isLoading}
    >
      {publication ? 'Publish again' : `Publish to request ${subject}`}
    </Button>
  )

  if (publication?.outcome === PokerRoundPublicationOutcome.Refused) {
    return (
      <Alert
        type="warning"
        showIcon
        title="Not published"
        description={publication.refusalReason}
        action={publishButton}
      />
    )
  }

  return (
    <Flex vertical align="center" gap={4}>
      {publishButton}
      <Text type="secondary" style={{ fontSize: 12 }}>
        {consensusIsCard
          ? `Not published. Publishing ${round.consensusEstimate} clears request ${subject}'s estimate.`
          : `Not published. Request ${subject} keeps its current estimate until this is.`}
      </Text>
    </Flex>
  )
}

export default RoundPublication
