'use client'

import { Spin } from 'antd'
import { memo, useState } from 'react'
import { StrategicInitiativeListDto } from '@/src/services/wayd-api'
import dynamic from 'next/dynamic'
import { useMessage } from '@/src/components/contexts/messaging'
import { StrategicInitiativesGrid } from '.'
import PpmViewSelector, { PpmView } from './ppm-view-selector'

const Timeline = dynamic(() => import('./strategic-initiatives-timeline'), {
  ssr: false,
  loading: () => <Spin />,
})

interface StrategicInitiativeViewManagerProps {
  strategicInitiatives: StrategicInitiativeListDto[]
  isLoading: boolean
  refetch: () => void
}

const StrategicInitiativeViewManager = (
  props: StrategicInitiativeViewManagerProps,
) => {
  const [currentView, setCurrentView] = useState<PpmView>('List')

  const messageApi = useMessage()

  const refreshWithFeedback = async () => {
    await props.refetch()
    messageApi.success('Timeline refreshed.')
  }

  const viewSelector = (
    <PpmViewSelector value={currentView} onChange={setCurrentView} />
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
          persistStateKey="portfolio-strategic-initiatives"
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
