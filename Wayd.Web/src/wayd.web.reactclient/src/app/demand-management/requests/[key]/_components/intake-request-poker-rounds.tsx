'use client'

import {
  PokerRoundPublicationOutcome,
  PokerRoundSubjectKind,
  PokerSubjectRoundDto,
} from '@/src/services/wayd-api'
import { useGetPokerRoundsForSubjectQuery } from '@/src/store/features/planning/poker-sessions-api'
import { Flex, Tag, Tooltip, Typography } from 'antd'
import dayjs from 'dayjs'
import NextLink from 'next/link'
import { LabeledContent } from '@/src/components/common/content'
import { FC } from 'react'
import { pokerSessionHref } from '../../../_components/desk-poker'

const { Text } = Typography

/** What a round did to the request, in a few words. */
export const roundOutcome = (round: PokerSubjectRoundDto): string => {
  if (round.status !== 'Accepted') return 'In progress'
  const consensus = round.consensusEstimate ?? ''
  switch (round.publication?.outcome) {
    case PokerRoundPublicationOutcome.Published:
      return `${consensus} published ${dayjs(round.publication.at).format('MMM D, YYYY')}`
    case PokerRoundPublicationOutcome.Refused:
      return `${consensus} not published`
    default:
      return `${consensus} not published yet`
  }
}

export interface IntakeRequestPokerRoundsProps {
  requestId: string
  /** Wraps the list as a labeled fact, so the label is absent when the list is. */
  labeled?: boolean
}

/**
 * The poker rounds that estimated the request, each linking to its session.
 * Lists only rounds in sessions the reader can open, and renders nothing when
 * there are none. Mount it only for a reader who may open Planning Poker.
 */
const IntakeRequestPokerRounds: FC<IntakeRequestPokerRoundsProps> = ({
  requestId,
  labeled,
}) => {
  const { data: rounds } = useGetPokerRoundsForSubjectQuery({
    subjectKind: PokerRoundSubjectKind.IntakeRequest,
    subjectId: requestId,
  })

  if (!rounds || rounds.length === 0) return null

  const list = (
    <Flex vertical gap={4}>
      {rounds.map((round) => (
        <Flex key={round.roundId} vertical>
          <NextLink href={pokerSessionHref(round.session.key)}>
            {round.session.name}
          </NextLink>
          <Tooltip title={round.publication?.refusalReason}>
            <Text type="secondary" style={{ fontSize: 12 }}>
              {roundOutcome(round)}
              {round.publication?.outcome ===
                PokerRoundPublicationOutcome.Refused && (
                <Tag color="warning" style={{ marginInlineStart: 6 }}>
                  Refused
                </Tag>
              )}
            </Text>
          </Tooltip>
        </Flex>
      ))}
    </Flex>
  )

  return labeled ? (
    <LabeledContent label="Poker Rounds">{list}</LabeledContent>
  ) : (
    list
  )
}

export default IntakeRequestPokerRounds
