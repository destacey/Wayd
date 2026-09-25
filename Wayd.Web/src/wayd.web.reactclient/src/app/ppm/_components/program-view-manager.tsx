'use client'

import { Spin } from 'antd'
import { memo, useState } from 'react'
import { ProgramListDto } from '@/src/services/wayd-api'
import dynamic from 'next/dynamic'
import { useMessage } from '@/src/components/contexts/messaging'
import ProgramsGrid from './programs-grid'
import PpmViewSelector, { PpmView } from './ppm-view-selector'

const Timeline = dynamic(() => import('./programs-timeline'), {
  ssr: false,
  loading: () => <Spin />,
})

interface ProgramViewManagerProps {
  programs: ProgramListDto[]
  isLoading: boolean
  refetch: () => void
}

const ProgramViewManager = (props: ProgramViewManagerProps) => {
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
        <ProgramsGrid
          programs={props.programs}
          isLoading={props.isLoading}
          refetch={props.refetch}
          hidePortfolio={true}
          viewSelector={viewSelector}
          persistStateKey="portfolio-programs"
        />
      )}
      {currentView === 'Timeline' && (
        <Timeline
          programs={props.programs}
          isLoading={props.isLoading}
          refetch={props.refetch}
          viewSelector={viewSelector}
          onRefresh={refreshWithFeedback}
        />
      )}
    </>
  )
}

export default memo(ProgramViewManager)
