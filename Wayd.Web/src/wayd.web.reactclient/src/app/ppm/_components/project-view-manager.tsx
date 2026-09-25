'use client'

import { Spin } from 'antd'
import { memo, useState } from 'react'
import { ProjectListDto } from '@/src/services/wayd-api'
import dynamic from 'next/dynamic'
import { useMessage } from '@/src/components/contexts/messaging'
import ProjectsGrid from './projects-grid'
import ProjectsCardView from './projects-card-view'
import ProjectDrawer from './project-drawer'
import PpmViewSelector, { PpmView } from './ppm-view-selector'

const Timeline = dynamic(() => import('./projects-timeline'), {
  ssr: false,
  loading: () => <Spin />,
})

type ProjectView = PpmView

interface ProjectViewManagerProps {
  projects: ProjectListDto[]
  isLoading: boolean
  refetch: () => void
  hidePortfolio?: boolean
  hideProgram?: boolean
  groupByProgram?: boolean
  defaultView?: ProjectView
  /** Column layout persistence key for the list view (see WaydGridProps). */
  persistStateKey?: string
}

const ProjectViewManager = (props: ProjectViewManagerProps) => {
  const [currentView, setCurrentView] = useState<ProjectView>(
    props.defaultView ?? 'List',
  )
  const [selectedProjectKey, setSelectedProjectKey] = useState<string | null>(
    null,
  )
  const [drawerOpen, setDrawerOpen] = useState(false)

  const messageApi = useMessage()

  const refreshWithFeedback = async () => {
    await props.refetch()
    messageApi.success('Timeline refreshed.')
  }

  const onCardClick = (key: string) => {
    setSelectedProjectKey(key)
    setDrawerOpen(true)
  }

  const viewSelector = (
    <PpmViewSelector
      views={['Card', 'List', 'Timeline']}
      value={currentView}
      onChange={setCurrentView}
    />
  )

  return (
    <>
      {currentView === 'Card' && (
        <ProjectsCardView
          projects={props.projects}
          isLoading={props.isLoading}
          viewSelector={viewSelector}
          onCardClick={onCardClick}
          hidePortfolio={props.hidePortfolio}
          hideProgram={props.hideProgram}
        />
      )}
      {currentView === 'List' && (
        <ProjectsGrid
          projects={props.projects}
          isLoading={props.isLoading}
          refetch={props.refetch}
          hidePortfolio={props.hidePortfolio}
          hideProgram={props.hideProgram}
          viewSelector={viewSelector}
          persistStateKey={props.persistStateKey}
        />
      )}
      {currentView === 'Timeline' && (
        <Timeline
          projects={props.projects}
          isLoading={props.isLoading}
          refetch={props.refetch}
          viewSelector={viewSelector}
          groupByProgram={props.groupByProgram}
          onRefresh={refreshWithFeedback}
        />
      )}
      {selectedProjectKey && (
        <ProjectDrawer
          projectKey={selectedProjectKey}
          drawerOpen={drawerOpen}
          onDrawerClose={() => {
            setDrawerOpen(false)
            setSelectedProjectKey(null)
          }}
        />
      )}
    </>
  )
}

export default memo(ProjectViewManager)
