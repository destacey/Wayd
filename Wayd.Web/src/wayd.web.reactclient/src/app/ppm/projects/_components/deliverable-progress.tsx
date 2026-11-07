'use client'

import { Progress, Typography } from 'antd'

const { Text } = Typography

export interface DeliverableProgressProps {
  /** The deliverable's derived progress, 0 to 100; null when no task is linked. */
  progress: number | null | undefined
  /** Room for the bar, in pixels. */
  width?: number
}

/**
 * How far the plan work serving a deliverable has got, as a small bar.
 * A deliverable with no linked task has no progress, which is said in words
 * rather than drawn as an empty bar that would read as 0%.
 */
const DeliverableProgress = ({
  progress,
  width = 140,
}: DeliverableProgressProps) => {
  if (progress === null || progress === undefined) {
    return <Text type="secondary">No linked tasks</Text>
  }

  const percent = Math.round(progress)
  return (
    <Progress
      percent={percent}
      size="small"
      style={{ width, margin: 0 }}
      aria-label={`${percent}% of the linked plan work done`}
    />
  )
}

export default DeliverableProgress
