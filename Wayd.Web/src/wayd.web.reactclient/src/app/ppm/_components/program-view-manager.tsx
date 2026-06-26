'use client'

import { BuildOutlined, MenuOutlined } from '@ant-design/icons'
import Segmented, { SegmentedLabeledOption } from 'antd/es/segmented'
import { Spin } from 'antd'
import { memo, useState } from 'react'
import { ProgramListDto } from '@/src/services/wayd-api'
import dynamic from 'next/dynamic'
import { useFeatureFlag } from '@/src/hooks'
import { useMessage } from '@/src/components/contexts/messaging'
import ProgramsGrid from './programs-grid'

const LegacyTimeline = dynamic(() => import('./programs-timeline'), {
  ssr: false,
  loading: () => <Spin />,
})

const TimelineV2 = dynamic(() => import('./programs-timeline-v2'), {
  ssr: false,
  loading: () => <Spin />,
})

interface ProgramViewManagerProps {
  programs: ProgramListDto[]
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

const ProgramViewManager = (props: ProgramViewManagerProps) => {
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
        <ProgramsGrid
          programs={props.programs}
          isLoading={props.isLoading}
          refetch={props.refetch}
          hidePortfolio={true}
          viewSelector={viewSelector}
        />
      )}
      {currentView === 'Timeline' && (isFlagLoading || useNewTimeline ? (
        <TimelineV2
          programs={props.programs}
          isLoading={props.isLoading}
          refetch={props.refetch}
          viewSelector={viewSelector}
          onRefresh={refreshWithFeedback}
        />
      ) : (
        <LegacyTimeline
          programs={props.programs}
          isLoading={props.isLoading}
          refetch={props.refetch}
          viewSelector={viewSelector}
        />
      ))}
    </>
  )
}

export default memo(ProgramViewManager)
