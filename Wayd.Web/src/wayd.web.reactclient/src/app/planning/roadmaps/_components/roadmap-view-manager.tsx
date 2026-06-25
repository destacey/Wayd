'use client'

import { RoadmapDetailsDto, RoadmapItemListDto } from '@/src/services/wayd-api'
import { BuildOutlined, MenuOutlined } from '@ant-design/icons'
import Segmented, { SegmentedLabeledOption } from 'antd/es/segmented'
import { memo, useState } from 'react'
import dynamic from 'next/dynamic'
import { Spin } from 'antd'
import { useFeatureFlag } from '@/src/hooks'
import { useMessage } from '@/src/components/contexts/messaging'
import RoadmapItemsGrid from './roadmap-items-grid'

// Legacy (vis-timeline) and new (in-house WaydTimeline2) roadmap timelines.
// Which one renders is gated by the "new-timeline-ui" feature flag below.
const RoadmapsTimeline = dynamic(() => import('./roadmaps-timeline'), {
  ssr: false,
  loading: () => <Spin />,
})

const RoadmapTimelineV2 = dynamic(() => import('./roadmap-timeline-v2'), {
  ssr: false,
  loading: () => <Spin />,
})

interface RoadmapViewManagerProps {
  roadmap: RoadmapDetailsDto
  roadmapItems: RoadmapItemListDto[]
  isRoadmapItemsLoading: boolean
  refreshRoadmapItems: () => void
  canUpdateRoadmap: boolean
  openRoadmapItemDrawer: (itemId: string) => void
}

const RoadmapViewManager = (props: RoadmapViewManagerProps) => {
  const [currentView, setCurrentView] = useState<string | number>('Timeline')

  const messageApi = useMessage()
  // Default-on flag (see FeatureFlags.cs). While flags load we keep the new UI
  // to avoid a flash of the legacy timeline on the happy path.
  const { isEnabled: useNewTimeline, isLoading: isFlagLoading } =
    useFeatureFlag('new-timeline-ui')

  const viewSelectorOptions: SegmentedLabeledOption[] = [
    {
      value: 'Timeline',
      icon: <BuildOutlined alt="Timeline" title="Timeline" />,
    },
    {
      value: 'List',
      icon: <MenuOutlined alt="List" title="List" />,
    },
  ]

  const viewSelector = (
    <Segmented
      options={viewSelectorOptions}
      value={currentView}
      onChange={setCurrentView}
    />
  )

  // The new timeline's Refresh button has no other visible signal when data
  // already exists (the chart looks identical after refetching unchanged data),
  // so confirm the action here. We await the refetch so the toast lands after it
  // settles. Failures surface through the query's own error state, not a throw.
  const refreshTimelineWithFeedback = async () => {
    await props.refreshRoadmapItems()
    messageApi.success('Timeline refreshed.')
  }

  return (
    <>
      {currentView === 'Timeline' &&
        (isFlagLoading || useNewTimeline ? (
          <RoadmapTimelineV2
            roadmap={props.roadmap}
            roadmapItems={props.roadmapItems}
            isRoadmapItemsLoading={props.isRoadmapItemsLoading}
            refreshRoadmapItems={refreshTimelineWithFeedback}
            viewSelector={viewSelector}
            openRoadmapItemDrawer={props.openRoadmapItemDrawer}
            isRoadmapManager={props.canUpdateRoadmap}
          />
        ) : (
          <RoadmapsTimeline
            roadmap={props.roadmap}
            roadmapItems={props.roadmapItems}
            isRoadmapItemsLoading={props.isRoadmapItemsLoading}
            refreshRoadmapItems={props.refreshRoadmapItems}
            viewSelector={viewSelector}
            openRoadmapItemDrawer={props.openRoadmapItemDrawer}
            isRoadmapManager={props.canUpdateRoadmap}
          />
        ))}
      {currentView === 'List' && (
        <RoadmapItemsGrid
          roadmapItemsData={props.roadmapItems}
          roadmapItemsIsLoading={props.isRoadmapItemsLoading}
          refreshRoadmapItems={props.refreshRoadmapItems}
          viewSelector={viewSelector}
          roadmapId={props.roadmap.id}
          openRoadmapItemDrawer={props.openRoadmapItemDrawer}
          isRoadmapManager={props.canUpdateRoadmap}
        />
      )}
    </>
  )
}

export default memo(RoadmapViewManager)
