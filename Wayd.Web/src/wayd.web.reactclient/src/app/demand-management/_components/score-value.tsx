'use client'

import { IntakeRequestCurrentScoreDto } from '@/src/services/wayd-api'
import { Tooltip, Typography } from 'antd'
import { FC } from 'react'

const { Text } = Typography

/**
 * A request's current score in a list or on a card. A score from a model its desk no longer uses is muted,
 * because it does not rank with the desk's other requests.
 */
export const ScoreValue: FC<{
  score: IntakeRequestCurrentScoreDto | undefined
}> = ({ score }) => {
  if (!score) return null
  const value = score.value.toFixed(2)
  return score.isDeskModel ? (
    <Tooltip title={`Scored with ${score.scoringModel.name}`}>
      <Text>{value}</Text>
    </Tooltip>
  ) : (
    <Tooltip
      title={`Scored with ${score.scoringModel.name}, which this desk no longer uses, so it does not rank with the desk's other requests.`}
    >
      <Text type="secondary">{value}</Text>
    </Tooltip>
  )
}
