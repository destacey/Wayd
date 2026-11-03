'use client'

import { IntakeRequestEstimateDto } from '@/src/services/wayd-api'
import { Tag, Tooltip } from 'antd'
import { FC } from 'react'
import { estimateScaleDescription } from './request-estimates'

/** A request's estimate in a list, on a card or in its facts, naming the scale it was given on. */
export const EstimateValue: FC<{
  estimate: IntakeRequestEstimateDto | undefined
}> = ({ estimate }) => {
  if (!estimate) return null
  return (
    <Tooltip title={estimateScaleDescription(estimate)}>
      <Tag variant="outlined" style={{ marginInlineEnd: 0 }}>
        {estimate.value}
      </Tag>
    </Tooltip>
  )
}
