'use client'

import {
  WaydGrid,
  renderAssignedToLink,
  renderProjectLink,
  renderSprintLink,
  renderTeamLink,
  renderWorkItemLink,
  renderWorkStatusTag,
  workItemKeySort,
  workStatusCategorySort,
} from '@/src/components/common/wayd-grid'
import { WorkItemBacklogItemDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@tanstack/react-table'
import { useMemo } from 'react'
import WorkItemTagsCell from './work-item-tags-cell'

export interface WorkItemsBacklogGridProps {
  workItems: WorkItemBacklogItemDto[]
  hideTeamColumn: boolean
  isLoading: boolean
  refetch: () => void
}

const WorkItemsBacklogGrid = (props: WorkItemsBacklogGridProps) => {
  const { refetch } = props

  const columns = useMemo<ColumnDef<WorkItemBacklogItemDto, any>[]>(
    () => [
      { id: 'rank', accessorKey: 'rank', header: 'Rank', size: 125 },
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
        accessorKey: 'type',
        header: 'Type',
        size: 125,
        meta: { filterType: 'set' },
      },
      {
        id: 'storyPoints',
        accessorKey: 'storyPoints',
        header: 'SPs',
        size: 80,
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
        id: 'team',
        accessorKey: 'team.name',
        header: 'Team',
        meta: { hide: props.hideTeamColumn, filterEnableSet: true },
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
        meta: { filterEnableSet: true },
        cell: ({ row }) => renderProjectLink(row.original.project),
      },
      {
        id: 'tags',
        accessorFn: (row) => row.tags?.join(', ') ?? '',
        header: 'Tags',
        size: 200,
        cell: ({ row }) => <WorkItemTagsCell tags={row.original.tags} />,
      },
    ],
    [props.hideTeamColumn],
  )

  const refresh = async () => {
    refetch()
  }

  return (
    <WaydGrid
      columns={columns}
      data={props.workItems ?? []}
      onRefresh={refresh}
      isLoading={props.isLoading}
      csvFileName="work-items-backlog"
    />
  )
}

export default WorkItemsBacklogGrid
