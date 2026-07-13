'use client'

import { FC, ReactNode } from 'react'
import { ColumnDef } from '@tanstack/react-table'
import { CaretDownOutlined, CaretRightOutlined } from '@ant-design/icons'
import { Button, Flex } from 'antd'
import treeGridStyles from '@/src/components/common/wayd-grid/wayd-grid.module.css'
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
import type { WaydGridColumnMeta } from '@/src/components/common/wayd-grid'
import { WorkItemListDto } from '@/src/services/wayd-api'
import {
  WorkItemTreeNode,
  buildWorkItemTree,
} from '@/src/components/common/work/work-item-tree-utils'
import styles from './project-work-items-tree-grid.module.css'

export interface ProjectWorkItemsTreeGridProps {
  workItems: WorkItemListDto[]
  isLoading: boolean
  refetch: () => void
  hideProjectColumn?: boolean
  viewSelector?: ReactNode | undefined
  gridHeight?: number
}

const getColumns = (
  hideProjectColumn: boolean,
): ColumnDef<WorkItemTreeNode>[] => [
  {
    accessorKey: 'key',
    header: 'Key',
    size: 120,
    sortingFn: workItemKeySort,
    cell: ({ row }) =>
      renderWorkItemLink({
        key: row.original.key,
        workspaceKey: row.original.workspace.key,
        externalViewWorkItemUrl: row.original.externalViewWorkItemUrl,
      }),
  },
  {
    accessorKey: 'title',
    header: 'Title',
    size: 400,
    cell: ({ row }) => {
      const depth = row.depth
      return (
        <Flex align="center" gap={0} className={treeGridStyles.nameCell}>
          {Array.from({ length: depth }).map((_, index) => (
            <span key={index} className={treeGridStyles.indentSpacer} />
          ))}
          {row.getCanExpand() ? (
            <Button
              type="text"
              size="small"
              icon={
                row.getIsExpanded() ? (
                  <CaretDownOutlined />
                ) : (
                  <CaretRightOutlined />
                )
              }
              onClick={row.getToggleExpandedHandler()}
              className={treeGridStyles.expanderBtn}
            />
          ) : (
            <span className={treeGridStyles.indentSpacer} />
          )}
          <span className={styles.titleText}>{row.original.title}</span>
        </Flex>
      )
    },
  },
  {
    id: 'type',
    accessorFn: (row) => row.type?.name ?? '',
    header: 'Type',
    size: 125,
    meta: { filterType: 'set' },
  },
  {
    id: 'status',
    accessorFn: (row) => row.status ?? '',
    header: 'Status',
    size: 125,
    meta: { filterType: 'set' },
    cell: ({ row }) => renderWorkStatusTag(row.original),
  },
  {
    id: 'statusCategory',
    accessorFn: (row) => row.statusCategory?.name ?? '',
    header: 'Status Category',
    size: 140,
    sortingFn: workStatusCategorySort,
    meta: { filterType: 'set' },
  },
  {
    id: 'storyPoints',
    accessorFn: (row) => row.storyPoints ?? undefined,
    header: 'SPs',
    size: 100,
    enableGlobalFilter: false,
    sortingFn: 'basic',
    sortUndefined: -1,
    meta: {
      exportHeader: 'Story Points',
    } satisfies WaydGridColumnMeta,
  },
  {
    id: 'team',
    accessorFn: (row) => row.team?.name ?? '',
    header: 'Team',
    size: 150,
    meta: { filterType: 'set' },
    cell: ({ row }) => renderTeamLink(row.original.team),
  },
  {
    id: 'sprint',
    accessorFn: (row) => row.sprint?.name ?? '',
    header: 'Sprint',
    size: 150,
    meta: { filterType: 'set' },
    cell: ({ row }) =>
      renderSprintLink(row.original.sprint, { showTeamCode: false }),
  },
  {
    id: 'assignedTo',
    accessorFn: (row) => row.assignedTo?.name ?? '',
    header: 'Assigned To',
    size: 150,
    meta: { filterType: 'set' },
    cell: ({ row }) => renderAssignedToLink(row.original.assignedTo),
  },
  // Tags render as overflow-aware chips and filter via the multi-value set
  // panel (individual tags faceted from the displayed rows, tree children
  // included).
  createCsvColumn<WorkItemTreeNode>({
    id: 'tags',
    header: 'Tags',
    size: 200,
    getValues: (row) => row.tags ?? [],
  }),
  ...(hideProjectColumn
    ? []
    : [
        {
          id: 'project',
          accessorFn: (row) => row.project?.name ?? '',
          header: 'Project',
          size: 300,
          meta: { filterType: 'set' },
          cell: ({ row }) => renderProjectLink(row.original.project),
        } satisfies ColumnDef<WorkItemTreeNode>,
      ]),
]

const ProjectWorkItemsTreeGrid: FC<ProjectWorkItemsTreeGridProps> = ({
  workItems,
  isLoading,
  refetch,
  hideProjectColumn = false,
  viewSelector,
  gridHeight,
}) => {
  const treeData = buildWorkItemTree(workItems ?? [])

  const columns = getColumns(hideProjectColumn)

  return (
    <WaydGrid<WorkItemTreeNode>
      data={treeData}
      getSubRows={(row) => row.children}
      columns={columns}
      isLoading={isLoading}
      height={gridHeight}
      onRefresh={async () => refetch()}
      rightSlot={viewSelector}
      persistStateKey="project-work-items-tree"
      csvFileName="project-work-items"
      emptyMessage="No work items found"
    />
  )
}

export default ProjectWorkItemsTreeGrid
