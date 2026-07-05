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
  /** Column layout persistence key for the hosting page (see WaydGridProps). */
  persistStateKey?: string
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
      // Context-redundant columns are excluded from the defs (not meta.hide):
      // they never belong on the hosting page, so they shouldn't appear in
      // the column chooser or the persisted layout either.
      ...(props.hideParentColumn
        ? []
        : [
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
            } satisfies ColumnDef<WorkItemListDto, any>,
            {
              id: 'parentTitle',
              accessorKey: 'parent.title',
              header: 'Parent',
              size: 400,
            } satisfies ColumnDef<WorkItemListDto, any>,
          ]),
      {
        id: 'assignedTo',
        accessorKey: 'assignedTo.name',
        header: 'Assigned To',
        meta: { filterEnableSet: true },
        cell: ({ row }) => renderAssignedToLink(row.original.assignedTo),
      },
      ...(props.hideProjectColumn
        ? []
        : [
            {
              id: 'project',
              accessorKey: 'project.name',
              header: 'Project',
              size: 300,
              meta: { filterEnableSet: true },
              cell: ({ row }) => renderProjectLink(row.original.project),
            } satisfies ColumnDef<WorkItemListDto, any>,
          ]),
      // The stats columns stay on meta.hide (NOT def-exclusion): the grid's
      // initialSorting sorts by the hidden 'done' column, and TanStack drops
      // sort entries whose column has no def — excluding them would silently
      // change the default row order on non-stats pages.
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
    <WaydGrid
      height={props.gridHeight}
      columns={columns}
      data={props.workItems}
      onRefresh={refresh}
      isLoading={props.isLoading}
      initialSorting={[{ id: 'done', desc: true }]}
      onDisplayedRowsChange={props.onDisplayedRowsChange}
      rightSlot={props.viewSelector}
      persistStateKey={props.persistStateKey}
      csvFileName="work-items"
    />
  )
}

export default WorkItemsGrid
