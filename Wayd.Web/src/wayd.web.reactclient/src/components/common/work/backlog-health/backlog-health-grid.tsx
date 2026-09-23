'use client'

import {
  WaydGrid,
  createCsvColumn,
  renderAssignedToLink,
  renderProjectLink,
  renderSprintLink,
  renderWorkItemLink,
  renderWorkStatusTag,
  workItemKeySort,
  workStatusCategorySort,
} from '@/src/components/common/wayd-grid'
import { BacklogHealthWorkItemDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '../../wayd-grid-core'
import { FC } from 'react'

export interface BacklogHealthGridProps {
  workItems: BacklogHealthWorkItemDto[]
  isLoading: boolean
  refetch: () => void
  /** Column layout persistence key for the hosting page (see WaydGridProps). */
  persistStateKey?: string
}

const GRID_HEIGHT = 650

const columns: ColumnDef<BacklogHealthWorkItemDto, any>[] = [
  { id: 'rank', accessorKey: 'rank', header: 'Rank', size: 90 },
  {
    id: 'key',
    accessorKey: 'key',
    header: 'Key',
    sortFn: workItemKeySort,
    cell: ({ row }) =>
      renderWorkItemLink({
        key: row.original.key,
        workspaceKey: row.original.workspace.key,
        externalViewWorkItemUrl: row.original.externalViewWorkItemUrl,
      }),
  },
  { id: 'title', accessorKey: 'title', header: 'Title', size: 400 },
  createCsvColumn<BacklogHealthWorkItemDto>({
    id: 'flags',
    header: 'Flags',
    size: 300,
    getValues: (row) => row.flags.map((f) => f.name),
  }),
  {
    id: 'type',
    accessorKey: 'type',
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
    sortFn: workStatusCategorySort,
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
    id: 'assignedTo',
    accessorKey: 'assignedTo.name',
    header: 'Assigned To',
    meta: { filterEnableSet: true },
    cell: ({ row }) => renderAssignedToLink(row.original.assignedTo),
  },
  {
    id: 'parentKey',
    accessorKey: 'parent.key',
    header: 'Parent Key',
    sortFn: workItemKeySort,
    meta: { filterType: 'set' },
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
    size: 300,
  },
  {
    id: 'sprint',
    accessorKey: 'sprint.name',
    header: 'Sprint',
    meta: { filterEnableSet: true },
    cell: ({ row }) => renderSprintLink(row.original.sprint),
  },
  {
    id: 'project',
    accessorKey: 'project.name',
    header: 'Project',
    size: 250,
    meta: { filterEnableSet: true },
    cell: ({ row }) => renderProjectLink(row.original.project),
  },
  {
    id: 'created',
    accessorKey: 'created',
    header: 'Created',
    meta: { columnType: 'dateTime' },
  },
  {
    id: 'lastModified',
    accessorKey: 'lastModified',
    header: 'Last Modified',
    meta: { columnType: 'dateTime' },
  },
  {
    id: 'activated',
    accessorKey: 'activated',
    header: 'Activated',
    meta: { columnType: 'dateTime' },
  },
]

const BacklogHealthGrid: FC<BacklogHealthGridProps> = ({
  workItems,
  isLoading,
  refetch,
  persistStateKey,
}) => (
  <WaydGrid
    // Fixed: the report's tiles and checks sit above the grid, so filling the
    // remaining viewport always bottomed out at the auto-height floor.
    height={GRID_HEIGHT}
    columns={columns}
    data={workItems}
    onRefresh={async () => refetch()}
    isLoading={isLoading}
    initialSorting={[{ id: 'rank', desc: false }]}
    persistStateKey={persistStateKey}
    csvFileName="backlog-health"
  />
)

export default BacklogHealthGrid
