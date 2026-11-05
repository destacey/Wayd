'use client'

import { Flex, Tag } from 'antd'
import { CSSProperties, FC } from 'react'

export interface ParticipantTagsProps {
  isFacilitator: boolean
  isVoting: boolean
  style?: CSSProperties
}

/**
 * What a session participant does beyond taking part: runs it, votes, both or
 * neither. Renders nothing for someone who does neither.
 */
const ParticipantTags: FC<ParticipantTagsProps> = ({
  isFacilitator,
  isVoting,
  style,
}) => {
  if (!isFacilitator && !isVoting) return null

  return (
    <Flex gap={4} style={style}>
      {isFacilitator && <Tag style={{ margin: 0 }}>Facilitator</Tag>}
      {isVoting && (
        <Tag color="processing" style={{ margin: 0 }}>
          Voting
        </Tag>
      )}
    </Flex>
  )
}

export default ParticipantTags
