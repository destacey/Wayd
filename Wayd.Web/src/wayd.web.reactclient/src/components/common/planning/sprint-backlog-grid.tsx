'use client'

import {
  WaydGrid,
  createCsvColumn,
  renderAssignedToLink,
  renderProjectLink,
  renderSprintLink,
  renderTeamLink,
  renderWorkItemLink,
  renderWorkStatusTag,
  workItemKeySort,
  workStatusCategorySort,
} from '@/src/components/common/wayd-grid'
import { SprintBacklogItemDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@tanstack/react-table'

export interface SprintBacklogGridProps {
  workItems: SprintBacklogItemDto[]
  isLoading: boolean
  refetch: () => void
  hideTeamColumn?: boolean
  hideSprintColumn?: boolean
  gridHeight?: number
  /** Column layout persistence key for the hosting page (see WaydGridProps). */
  persistStateKey?: string
}

const SprintBacklogGrid = (props: SprintBacklogGridProps) => {
  const {
    workItems = [],
    refetch,
    hideTeamColumn = false,
    hideSprintColumn = false,
    gridHeight,
  } = props

  const columns: ColumnDef<SprintBacklogItemDto, any>[] = [
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
    // Context-redundant columns are excluded from the defs (not meta.hide):
    // they never belong on the hosting page, so they shouldn't appear in
    // the column chooser or the persisted layout either.
    ...(hideTeamColumn
      ? []
      : [
          {
            id: 'team',
            accessorKey: 'team.name',
            header: 'Team',
            meta: { filterEnableSet: true },
            cell: ({ row }) => renderTeamLink(row.original.team),
          } satisfies ColumnDef<SprintBacklogItemDto, any>,
        ]),
    ...(hideSprintColumn
      ? []
      : [
          {
            id: 'sprint',
            accessorKey: 'sprint.name',
            header: 'Sprint',
            meta: { filterEnableSet: true },
            cell: ({ row }) =>
              renderSprintLink(row.original.sprint, { showTeamCode: false }),
          } satisfies ColumnDef<SprintBacklogItemDto, any>,
        ]),
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
    createCsvColumn<SprintBacklogItemDto>({
      id: 'tags',
      header: 'Tags',
      size: 200,
      getValues: (row) => row.tags ?? [],
    }),
  ]

  const refresh = async () => {
    refetch()
  }

  return (
    <WaydGrid
      height={gridHeight}
      columns={columns}
      data={workItems ?? []}
      onRefresh={refresh}
      isLoading={props.isLoading}
      persistStateKey={props.persistStateKey}
      csvFileName="sprint-backlog"
      emptyMessage="No planned work items"
    />
  )
}

export default SprintBacklogGrid
