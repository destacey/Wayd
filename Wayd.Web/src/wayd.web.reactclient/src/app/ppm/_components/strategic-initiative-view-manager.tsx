'use client'

import { BuildOutlined, MenuOutlined } from '@ant-design/icons'
import Segmented, { SegmentedLabeledOption } from 'antd/es/segmented'
import { Spin } from 'antd'
import { memo, useState } from 'react'
import { StrategicInitiativeListDto } from '@/src/services/wayd-api'
import dynamic from 'next/dynamic'
import { useFeatureFlag } from '@/src/hooks'
import { useMessage } from '@/src/components/contexts/messaging'
import { StrategicInitiativesGrid } from '.'

const LegacyTimeline = dynamic(
  () => import('./strategic-initiatives-timeline'),
  {
    ssr: false,
    loading: () => <Spin />,
  },
)

const TimelineV2 = dynamic(
  () => import('./strategic-initiatives-timeline-v2'),
  {
    ssr: false,
    loading: () => <Spin />,
  },
)

interface StrategicInitiativeViewManagerProps {
  strategicInitiatives: StrategicInitiativeListDto[]
  isLoading: boolean
  refetch: () => void
}

const viewSelectorOptions: SegmentedLabeledOption[] = [
  {
    value: 'List',
    icon: <MenuOutlined alt="List" title="List" />,
  },
  {
    value: 'Timeline',
    icon: <BuildOutlined alt="Timeline" title="Timeline" />,
  },
]

const StrategicInitiativeViewManager = (
  props: StrategicInitiativeViewManagerProps,
) => {
  const [currentView, setCurrentView] = useState<string | number>('List')

  const { isEnabled: useNewTimeline, isLoading: isFlagLoading } =
    useFeatureFlag('new-timeline-ui')
  const messageApi = useMessage()

  const refreshWithFeedback = async () => {
    await props.refetch()
    messageApi.success('Timeline refreshed.')
  }

  const viewSelector = (
    <Segmented
      options={viewSelectorOptions}
      value={currentView}
      onChange={setCurrentView}
    />
  )

  return (
    <>
      {currentView === 'List' && (
        <StrategicInitiativesGrid
          strategicInitiatives={props.strategicInitiatives}
          isLoading={props.isLoading}
          refetch={props.refetch}
          hidePortfolio={true}
          gridHeight={550}
          viewSelector={viewSelector}
        />
      )}
      {currentView === 'Timeline' && (isFlagLoading || useNewTimeline ? (
        <TimelineV2
          strategicInitiatives={props.strategicInitiatives}
          isLoading={props.isLoading}
          refetch={props.refetch}
          viewSelector={viewSelector}
          onRefresh={refreshWithFeedback}
        />
      ) : (
        <LegacyTimeline
          strategicInitiatives={props.strategicInitiatives}
          isLoading={props.isLoading}
          refetch={props.refetch}
          viewSelector={viewSelector}
        />
      ))}
    </>
  )
}

export default memo(StrategicInitiativeViewManager)
