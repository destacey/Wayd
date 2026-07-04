'use client'

import { BuildOutlined, MenuOutlined } from '@ant-design/icons'
import Segmented, { SegmentedLabeledOption } from 'antd/es/segmented'
import { Spin } from 'antd'
import { memo, useState } from 'react'
import { StrategicInitiativeListDto } from '@/src/services/wayd-api'
import dynamic from 'next/dynamic'
import { useMessage } from '@/src/components/contexts/messaging'
import { StrategicInitiativesGrid } from '.'

const Timeline = dynamic(() => import('./strategic-initiatives-timeline'), {
  ssr: false,
  loading: () => <Spin />,
})

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
          viewSelector={viewSelector}
        />
      )}
      {currentView === 'Timeline' && (
        <Timeline
          strategicInitiatives={props.strategicInitiatives}
          isLoading={props.isLoading}
          refetch={props.refetch}
          viewSelector={viewSelector}
          onRefresh={refreshWithFeedback}
        />
      )}
    </>
  )
}

export default memo(StrategicInitiativeViewManager)
