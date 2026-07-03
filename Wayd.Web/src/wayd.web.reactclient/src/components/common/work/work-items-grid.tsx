'use client'

import {
  WaydGrid2,
  renderAssignedToLink,
  renderProjectLink,
  renderSprintLink,
  renderTeamLink,
  renderWorkItemLink,
  renderWorkStatusTag,
  workItemKeySort,
  workStatusCategorySort,
} from '@/src/components/common/wayd-grid2'
import { WorkItemListDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@tanstack/react-table'
import { FC, ReactNode, useMemo } from 'react'

export interface WorkItemsGridProps {
  workItems: WorkItemListDto[]
  isLoading: boolean
  refetch: () => void
  gridHeight?: number
  hideParentColumn?: boolean
  hideProjectColumn?: boolean
  showStats?: boolean
  /** Fires with the displayed (post filter + sort) rows whenever that set
   *  changes — e.g. the cycle-time report syncs its chart to the grid. */
  onDisplayedRowsChange?: (workItems: WorkItemListDto[]) => void
  viewSelector?: ReactNode | undefined
}

const WorkItemsGrid: FC<WorkItemsGridProps> = (props) => {
  const { refetch } = props

  const columns = useMemo<ColumnDef<WorkItemListDto, any>[]>(
    () => [
      {
        id: 'key',
        accessorKey: 'key',
        header: 'Key',
        sortingFn: workItemKeySort,
        cell: ({ row }) =>
          renderWorkItemLink({
            key: row.original.key,
            workspaceKey: row.original.workspace.key,
            externalViewWorkItemUrl: row.original.externalViewWorkItemUrl,
          }),
      },
      { id: 'title', accessorKey: 'title', header: 'Title', size: 400 },
      {
        id: 'type',
        accessorKey: 'type.name',
        header: 'Type',
        size: 125,
        meta: { filterType: 'set' },
      },
      {
        id: 'status',
        accessorKey: 'status',
        header: 'Status',
        size: 125,
        meta: { filterType: 'set' },
        cell: ({ row }) => renderWorkStatusTag(row.original),
      },
      {
        id: 'statusCategory',
        accessorKey: 'statusCategory.name',
        header: 'Status Category',
        size: 140,
        sortingFn: workStatusCategorySort,
        meta: { filterType: 'set' },
      },
      {
        id: 'storyPoints',
        accessorKey: 'storyPoints',
        header: 'SPs',
        size: 100,
        meta: { headerTooltip: 'Story Points' },
      },
      {
        id: 'team',
        accessorKey: 'team.name',
        header: 'Team',
        meta: { filterEnableSet: true },
        cell: ({ row }) => renderTeamLink(row.original.team),
      },
      {
        id: 'sprint',
        accessorKey: 'sprint.name',
        header: 'Sprint',
        meta: { filterEnableSet: true },
        cell: ({ row }) => renderSprintLink(row.original.sprint),
      },
      {
        id: 'parentKey',
        accessorKey: 'parent.key',
        header: 'Parent Key',
        sortingFn: workItemKeySort,
        meta: { hide: props.hideParentColumn },
        cell: ({ row }) =>
          renderWorkItemLink(
            row.original.parent
              ? {
                  key: row.original.parent.key,
                  workspaceKey: row.original.parent.workspaceKey,
                  externalViewWorkItemUrl:
                    row.original.parent.externalViewWorkItemUrl,
                }
              : null,
          ),
      },
      {
        id: 'parentTitle',
        accessorKey: 'parent.title',
        header: 'Parent',
        size: 400,
        meta: { hide: props.hideParentColumn },
      },
      {
        id: 'assignedTo',
        accessorKey: 'assignedTo.name',
        header: 'Assigned To',
        meta: { filterEnableSet: true },
        cell: ({ row }) => renderAssignedToLink(row.original.assignedTo),
      },
      {
        id: 'project',
        accessorKey: 'project.name',
        header: 'Project',
        size: 300,
        meta: { hide: props.hideProjectColumn, filterEnableSet: true },
        cell: ({ row }) => renderProjectLink(row.original.project),
      },
      {
        id: 'activated',
        accessorKey: 'activated',
        header: 'Activated',
        meta: { hide: !props.showStats, columnType: 'dateTime' },
      },
      {
        id: 'done',
        accessorKey: 'done',
        header: 'Done',
        meta: { hide: !props.showStats, columnType: 'dateTime' },
      },
      {
        id: 'cycleTime',
        accessorKey: 'cycleTime',
        header: 'Cycle Time (Days)',
        meta: { hide: !props.showStats },
        cell: ({ getValue }) => getValue<number | undefined>()?.toFixed(2) ?? '',
      },
    ],
    [props.hideParentColumn, props.hideProjectColumn, props.showStats],
  )

  const refresh = async () => {
    refetch()
  }

  return (
    <WaydGrid2
      height={props.gridHeight}
      columns={columns}
      data={props.workItems}
      onRefresh={refresh}
      isLoading={props.isLoading}
      initialSorting={[{ id: 'done', desc: true }]}
      onDisplayedRowsChange={props.onDisplayedRowsChange}
      rightSlot={props.viewSelector}
      csvFileName="work-items"
    />
  )
}

export default WorkItemsGrid
