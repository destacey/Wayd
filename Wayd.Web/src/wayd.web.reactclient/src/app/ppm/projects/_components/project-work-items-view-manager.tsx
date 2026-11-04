'use client'

import { ClusterOutlined, MenuOutlined } from '@ant-design/icons'
import type { SegmentedLabeledOption } from 'antd/es/segmented'
import { App, Segmented } from 'antd'
import type { ItemType } from 'antd/es/menu/interface'
import { memo, useState } from 'react'
import { WorkItemListDto } from '@/src/services/wayd-api'
import ProjectWorkItemsTreeGrid from './project-work-items-tree-grid'
import WorkItemsGrid from '@/src/components/common/work/work-items-grid'
import { useMessage } from '@/src/components/contexts/messaging'
import { useRemoveProjectWorkItemsMutation } from '@/src/store/features/ppm/projects-api'
import { toApiError } from '@/src/utils'

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
  /** The project the list belongs to; with `canManageWorkItems`, enables removing work items. */
  projectId?: string
  canManageWorkItems?: boolean
}

const ProjectWorkItemsViewManager = (
  props: ProjectWorkItemsViewManagerProps,
) => {
  const [currentView, setCurrentView] = useState<string | number>(
    props.defaultView ?? 'Tree',
  )
  const { modal } = App.useApp()
  const messageApi = useMessage()
  const [removeProjectWorkItems] = useRemoveProjectWorkItemsMutation()

  const { projectId } = props
  const canManage = !!projectId && !!props.canManageWorkItems

  const workItemIds = new Set(props.workItems.map((w) => w.id))

  // Only a Portfolio-tier work item holds a project of its own, and it does so exactly when its parent
  // is not on the project too; everything else inherits its parent's link.
  const isLinkedDirectly = (workItem: WorkItemListDto) =>
    !workItem.parent || !workItemIds.has(workItem.parent.id)

  const confirmRemove = (workItem: WorkItemListDto) => {
    if (!projectId) return
    modal.confirm({
      title: `Remove ${workItem.key} from this project?`,
      content:
        'The work items beneath it leave the project too, unless they have a project of their own. If its parent is on another project, it moves to that one.',
      okText: 'Remove',
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await removeProjectWorkItems({
            projectId,
            workItemIds: [workItem.id],
          }).unwrap()
          messageApi.success(`${workItem.key} removed from the project.`)
        } catch (error) {
          messageApi.error(
            toApiError(error).detail ??
              'Failed to remove the work item from the project.',
          )
        }
      },
    })
  }

  const getRowActions = canManage
    ? (workItem: WorkItemListDto): ItemType[] =>
        isLinkedDirectly(workItem)
          ? [
              {
                key: 'remove',
                label: 'Remove from Project',
                danger: true,
                onClick: () => confirmRemove(workItem),
              },
            ]
          : []
    : undefined

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
          getRowActions={getRowActions}
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
          getRowActions={getRowActions}
        />
      )}
    </>
  )
}

export default memo(ProjectWorkItemsViewManager)
