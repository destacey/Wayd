'use client'

import { ClusterOutlined, MenuOutlined } from '@ant-design/icons'
import Segmented, { SegmentedLabeledOption } from 'antd/es/segmented'
import { memo, useState } from 'react'
import { WorkItemListDto } from '@/src/services/wayd-api'
import ProjectWorkItemsTreeGrid from './project-work-items-tree-grid'
import WorkItemsGrid from '@/src/components/common/work/work-items-grid'

type WorkItemsView = 'Tree' | 'List'

const viewSelectorOptions: SegmentedLabeledOption[] = [
  {
    value: 'Tree',
    icon: <ClusterOutlined alt="Tree view" title="Tree view" />,
  },
  {
    value: 'List',
    icon: <MenuOutlined alt="List view" title="List view" />,
  },
]

interface ProjectWorkItemsViewManagerProps {
  workItems: WorkItemListDto[]
  isLoading: boolean
  refetch: () => void
  hideProjectColumn?: boolean
  defaultView?: WorkItemsView
  gridHeight?: number
}

const ProjectWorkItemsViewManager = (
  props: ProjectWorkItemsViewManagerProps,
) => {
  const [currentView, setCurrentView] = useState<string | number>(
    props.defaultView ?? 'Tree',
  )

  const viewSelector = (
      <Segmented
        options={viewSelectorOptions}
        value={currentView}
        onChange={setCurrentView}
      />
    )

  return (
    <>
      {currentView === 'Tree' && (
        <ProjectWorkItemsTreeGrid
          workItems={props.workItems}
          isLoading={props.isLoading}
          refetch={props.refetch}
          hideProjectColumn={props.hideProjectColumn}
          viewSelector={viewSelector}
          gridHeight={props.gridHeight}
        />
      )}
      {currentView === 'List' && (
        <WorkItemsGrid
          workItems={props.workItems}
          isLoading={props.isLoading}
          refetch={props.refetch}
          hideProjectColumn={props.hideProjectColumn}
          viewSelector={viewSelector}
          gridHeight={props.gridHeight}
          persistStateKey="project-work-items"
        />
      )}
    </>
  )
}

export default memo(ProjectWorkItemsViewManager)
