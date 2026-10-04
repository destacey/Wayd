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
import { BacklogHealthWorkItemDto, SizingMethod } from '@/src/services/wayd-api'
import { sizingMethodLabel } from '@/src/utils'
import type { ColumnDef } from '../../wayd-grid-core'
import { FC } from 'react'

export interface BacklogHealthGridProps {
  workItems: BacklogHealthWorkItemDto[]
  /** The team's sizing method, which the estimate column is in. A team that sizes by count has no estimate column. */
  sizingMethod?: SizingMethod
  isLoading: boolean
  refetch: () => void
  /** Column layout persistence key for the hosting page (see WaydGridProps). */
  persistStateKey?: string
}

const GRID_HEIGHT = 650

const estimateColumn = (
  sizingMethod: SizingMethod,
): ColumnDef<BacklogHealthWorkItemDto, any> => ({
  id: 'estimate',
  accessorKey: 'estimate',
  header: sizingMethodLabel(sizingMethod),
  size: 100,
  meta: {
    headerTooltip: `The team's estimate: ${sizingMethodLabel(sizingMethod)}`,
  },
})

// Built once per sizing method, so the grid receives the same column array on every render.
const columnsBySizingMethod = new Map<
  SizingMethod,
  ColumnDef<BacklogHealthWorkItemDto, any>[]
>()

const columnsFor = (sizingMethod: SizingMethod) => {
  let columns = columnsBySizingMethod.get(sizingMethod)
  if (!columns) {
    columns = baseColumns.flatMap((column) =>
      column.id !== ESTIMATE_SLOT
        ? [column]
        : sizingMethod === SizingMethod.Count
          ? []
          : [estimateColumn(sizingMethod)],
    )
    columnsBySizingMethod.set(sizingMethod, columns)
  }
  return columns
}

const ESTIMATE_SLOT = 'estimate'

const baseColumns: ColumnDef<BacklogHealthWorkItemDto, any>[] = [
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
  { id: ESTIMATE_SLOT },
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
  sizingMethod = SizingMethod.Count,
  isLoading,
  refetch,
  persistStateKey,
}) => (
  <WaydGrid
    // Fixed: the report's tiles and checks sit above the grid, so filling the
    // remaining viewport always bottomed out at the auto-height floor.
    height={GRID_HEIGHT}
    columns={columnsFor(sizingMethod)}
    data={workItems}
    onRefresh={async () => refetch()}
    isLoading={isLoading}
    initialSorting={[{ id: 'rank', desc: false }]}
    persistStateKey={persistStateKey}
    csvFileName="backlog-health"
  />
)

export default BacklogHealthGrid
