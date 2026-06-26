'use client'

// Picks between the legacy vis-timeline and new WaydTimeline2 implementations
// of the PI objectives timeline, gated by the "new-timeline-ui" feature flag.
// Default-on: while the flag loads we show the new timeline to avoid a flash of
// the legacy one on the happy path.

import { ReactNode } from 'react'
import { Spin } from 'antd'
import dynamic from 'next/dynamic'
import { useFeatureFlag } from '@/src/hooks'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  PlanningIntervalCalendarDto,
  PlanningIntervalObjectiveListDto,
} from '@/src/services/wayd-api'

const LegacyTimeline = dynamic(
  () => import('./planning-interval-objectives-timeline'),
  { ssr: false, loading: () => <Spin /> },
)

const TimelineV2 = dynamic(
  () => import('./planning-interval-objectives-timeline-v2'),
  { ssr: false, loading: () => <Spin /> },
)

export interface PlanningIntervalObjectivesViewManagerProps {
  objectivesData: PlanningIntervalObjectiveListDto[]
  planningIntervalCalendar: PlanningIntervalCalendarDto
  enableGroups?: boolean
  teamNames?: string[]
  viewSelector?: ReactNode
  onObjectiveClick?: (objectiveKey: number) => void
  onRefresh?: () => void
}

const PlanningIntervalObjectivesViewManager = (
  props: PlanningIntervalObjectivesViewManagerProps,
) => {
  const { isEnabled: useNewTimeline, isLoading: isFlagLoading } =
    useFeatureFlag('new-timeline-ui')
  const messageApi = useMessage()

  const refreshWithFeedback = props.onRefresh
    ? async () => {
        await props.onRefresh!()
        messageApi.success('Timeline refreshed.')
      }
    : undefined

  if (isFlagLoading || useNewTimeline) {
    return <TimelineV2 {...props} onRefresh={refreshWithFeedback} />
  }

  return <LegacyTimeline {...props} />
}

export default PlanningIntervalObjectivesViewManager
