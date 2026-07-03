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
import { SprintBacklogItemDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@tanstack/react-table'
import { useMemo } from 'react'
import { WorkItemTagsCell } from '@/src/components/common/work'

export interface SprintBacklogGridProps {
  workItems: SprintBacklogItemDto[]
  isLoading: boolean
  refetch: () => void
  hideTeamColumn?: boolean
  hideSprintColumn?: boolean
  /** Grid height in pixels. Use -1 for auto-height (expands to fit all rows). Default: -1 */
  gridHeight?: number
}

const SprintBacklogGrid = (props: SprintBacklogGridProps) => {
  const {
    workItems = [],
    refetch,
    hideTeamColumn = false,
    hideSprintColumn = false,
    gridHeight = -1,
  } = props

  const columns = useMemo<ColumnDef<SprintBacklogItemDto, any>[]>(
    () => [
      {
        id: 'rank',
        accessorKey: 'rank',
        header: 'Rank',
        size: 50,
        enableColumnFilter: false,
      },
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
      {
        id: 'type',
        accessorKey: 'type',
        header: 'Type',
        size: 125,
        meta: { filterType: 'set' },
      },
      { id: 'title', accessorKey: 'title', header: 'Title', size: 400 },
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
        size: 120,
        sortingFn: workStatusCategorySort,
        meta: { filterType: 'set' },
      },
      {
        id: 'team',
        accessorKey: 'team.name',
        header: 'Team',
        meta: { hide: hideTeamColumn, filterEnableSet: true },
        cell: ({ row }) => renderTeamLink(row.original.team),
      },
      {
        id: 'sprint',
        accessorKey: 'sprint.name',
        header: 'Sprint',
        meta: { hide: hideSprintColumn, filterEnableSet: true },
        cell: ({ row }) =>
          renderSprintLink(row.original.sprint, { showTeamCode: false }),
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
    [hideTeamColumn, hideSprintColumn],
  )

  const refresh = async () => {
    refetch()
  }

  return (
    <WaydGrid2
      height={gridHeight}
      columns={columns}
      data={workItems ?? []}
      onRefresh={refresh}
      isLoading={props.isLoading}
      csvFileName="sprint-backlog"
      emptyMessage="No planned work items"
    />
  )
}

export default SprintBacklogGrid
