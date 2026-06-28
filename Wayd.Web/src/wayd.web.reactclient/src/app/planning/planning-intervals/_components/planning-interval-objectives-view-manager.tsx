'use client'

import { ReactNode } from 'react'
import { Spin } from 'antd'
import dynamic from 'next/dynamic'
import { useMessage } from '@/src/components/contexts/messaging'
import {
  PlanningIntervalCalendarDto,
  PlanningIntervalObjectiveListDto,
} from '@/src/services/wayd-api'

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
  const messageApi = useMessage()

  const refreshWithFeedback = props.onRefresh
    ? async () => {
        await props.onRefresh!()
        messageApi.success('Timeline refreshed.')
      }
    : undefined

  return <TimelineV2 {...props} onRefresh={refreshWithFeedback} />
}

export default PlanningIntervalObjectivesViewManager
