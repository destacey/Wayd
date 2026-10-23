'use client'

import { WorkItemProgressRollupDto } from '@/src/services/wayd-api'
import { Progress } from 'antd'
import WaydTooltip from '@/src/components/common/wayd-tooltip'
import { memo } from 'react'

export interface WorkProgressProps {
  progress: WorkItemProgressRollupDto
}

interface ProgressSummary {
  proposed: number
  proposedPercentage: number
  active: number
  activePercentage: number
  done: number
  donePercentage: number
  total: number
}
const calculateProgressPercentages = (
  rollup: WorkItemProgressRollupDto,
): ProgressSummary => {
  if (rollup.total === 0) {
    return {
      proposed: 0,
      proposedPercentage: 0,
      active: 0,
      activePercentage: 0,
      done: 0,
      donePercentage: 0,
      total: 0,
    }
  }

  return {
    proposed: rollup.proposed,
    proposedPercentage: Math.round((rollup.proposed / rollup.total) * 10000) / 100,
    active: rollup.active,
    activePercentage: Math.round((rollup.active / rollup.total) * 10000) / 100,
    done: rollup.done,
    donePercentage: Math.round((rollup.done / rollup.total) * 10000) / 100,
    total: rollup.total,
  }
}

const WorkProgress = memo(({ progress }: WorkProgressProps) => {
  const progressSummary = progress
    ? calculateProgressPercentages(progress)
    : null

  const titleText = !progressSummary ? null : (
    <ul style={{ paddingLeft: 20 }}>
      <li>
        Proposed: {progressSummary.proposed} (
        {progressSummary.proposedPercentage}%)
      </li>
      <li>
        Active: {progressSummary.active} ({progressSummary.activePercentage}%)
      </li>
      <li>
        Done: {progressSummary.done} ({progressSummary.donePercentage}%)
      </li>
      <li>Total: {progressSummary.total}</li>
    </ul>
  )

  if (!progressSummary) return null

  return (
    <>
      {progressSummary.total > 0 && (
        <WaydTooltip title={titleText}>
          <Progress
            percent={
              progressSummary.activePercentage + progressSummary.donePercentage
            }
            format={() => `${progressSummary.donePercentage}% done`}
            percentPosition={{ align: 'center', type: 'outer' }}
            success={{ percent: progressSummary.donePercentage }}
          />
        </WaydTooltip>
      )}
    </>
  )
})

// Set display name for the memoized component
WorkProgress.displayName = 'WorkProgress'

export default WorkProgress
