'use client'

import { SizingMethod } from '@/src/services/wayd-api'
import { useGetSprintBurnQuery } from '@/src/store/features/work-management/sprints-api'
import { sizingMethodMeasure } from '@/src/utils'
import { theme } from 'antd'
import { FC } from 'react'
import BurnChart from './burn-chart'
import { BurnSeries, sprintBurnSeries } from './sprint-burn-series'

export interface SprintBurnUpProps {
  sprintKey: number
  sizingMethod: SizingMethod
}

/**
 * A small burn-up for a sprint summary card, in the sprint's own estimate.
 * Nothing until the sprint has readings, or while its history is incomplete:
 * a summary has no room to explain an empty chart.
 */
const SprintBurnUp: FC<SprintBurnUpProps> = ({ sprintKey, sizingMethod }) => {
  const { token } = theme.useToken()
  const { data: burn } = useGetSprintBurnQuery(sprintKey)

  if (!burn || burn.historyIncomplete || burn.points.length === 0) return null

  const { burnUp } = sprintBurnSeries(burn, sizingMethod === SizingMethod.Count)
  const measure = sizingMethodMeasure(sizingMethod)

  return (
    <BurnChart
      compact
      title="Burn-up"
      tooltip={`The ${measure} in the sprint and the part of it completed, day by day. A rising scope line is work added or re-estimated.`}
      data={burnUp}
      series={[BurnSeries.Scope, BurnSeries.Completed]}
      colors={[token.colorTextTertiary, token.colorSuccess]}
      timeZone={burn.timeZone}
      measure={measure}
      start={new Date(burn.effectiveStart)}
      end={new Date(burn.effectiveEnd)}
      isLoading={false}
    />
  )
}

export default SprintBurnUp
