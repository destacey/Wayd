'use client'

import { FC, ReactNode } from 'react'
import { ColumnDef } from '@tanstack/react-table'
import Link from 'next/link'
import {
  CaretDownOutlined,
  CaretRightOutlined,
  ExportOutlined,
} from '@ant-design/icons'
import { Button, Flex } from 'antd'
import treeGridStyles from '@/src/components/common/wayd-grid/wayd-grid.module.css'
import { WaydGrid, createCsvColumn } from '@/src/components/common/wayd-grid'
import type { WaydGridColumnMeta } from '@/src/components/common/wayd-grid'
import { WorkItemListDto } from '@/src/services/wayd-api'
import {
  WorkItemTreeNode,
  buildWorkItemTree,
} from '@/src/components/common/work/work-item-tree-utils'
import WorkStatusTag from '@/src/components/common/work/work-status-tag'
import styles from './project-work-items-tree-grid.module.css'

export interface ProjectWorkItemsTreeGridProps {
  workItems: WorkItemListDto[]
  isLoading: boolean
  refetch: () => void
  hideProjectColumn?: boolean
  viewSelector?: ReactNode | undefined
  gridHeight?: number
}

function WorkItemKeyCell({ item }: { item: WorkItemTreeNode }) {
  return (
    <>
      <Link
        href={`/work/workspaces/${item.workspace.key}/work-items/${item.key}`}
        prefetch={false}
      >
        {item.key}
      </Link>
      {item.externalViewWorkItemUrl && (
        <Link
          href={item.externalViewWorkItemUrl}
          target="_blank"
          title="Open in external system"
          style={{ marginLeft: '5px' }}
        >
          <ExportOutlined style={{ width: '10px' }} />
        </Link>
      )}
    </>
  )
}

function TeamCell({ item }: { item: WorkItemTreeNode }) {
  if (!item.team) return null
  const teamLink =
    item.team.type === 'Team'
      ? `/organizations/teams/${item.team.key}`
      : `/organizations/team-of-teams/${item.team.key}`
  return <Link href={teamLink}>{item.team.name}</Link>
}

function SprintCell({ item }: { item: WorkItemTreeNode }) {
  if (!item.sprint) return null
  return (
    <Link href={`/planning/sprints/${item.sprint.key}`}>
      {item.sprint.name}
    </Link>
  )
}

function AssignedToCell({ item }: { item: WorkItemTreeNode }) {
  if (!item.assignedTo) return null
  return (
    <Link
      href={`/organizations/employees/${item.assignedTo.key}`}
      prefetch={false}
    >
      {item.assignedTo.name}
    </Link>
  )
}

function ProjectCell({ item }: { item: WorkItemTreeNode }) {
  if (!item.project) return null
  return (
    <Link href={`/ppm/projects/${item.project.key}`}>{item.project.name}</Link>
  )
}

const getColumns = (
  hideProjectColumn: boolean,
): ColumnDef<WorkItemTreeNode>[] => [
  {
    accessorKey: 'key',
    header: 'Key',
    size: 120,
    enableGlobalFilter: true,
    enableColumnFilter: true,
    sortingFn: 'alphanumeric',
    cell: ({ row }) => <WorkItemKeyCell item={row.original} />,
  },
  {
    accessorKey: 'title',
    header: 'Title',
    size: 400,
    enableGlobalFilter: true,
    enableColumnFilter: true,
    sortingFn: 'text',
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
    enableGlobalFilter: true,
    enableColumnFilter: true,
    sortingFn: 'text',
  },
  {
    id: 'status',
    accessorFn: (row) => row.status ?? '',
    header: 'Status',
    size: 125,
    enableGlobalFilter: true,
    enableColumnFilter: true,
    sortingFn: 'text',
    cell: ({ row }) => {
      const item = row.original
      if (!item.status) return null
      return (
        <WorkStatusTag status={item.status} category={item.statusCategory.id} />
      )
    },
  },
  {
    id: 'statusCategory',
    accessorFn: (row) => row.statusCategory?.name ?? '',
    header: 'Status Category',
    size: 140,
    enableGlobalFilter: true,
    enableColumnFilter: true,
    sortingFn: 'text',
  },
  {
    id: 'storyPoints',
    accessorFn: (row) => row.storyPoints ?? undefined,
    header: 'SPs',
    size: 100,
    enableGlobalFilter: false,
    enableColumnFilter: true,
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
    enableGlobalFilter: true,
    enableColumnFilter: true,
    sortingFn: 'text',
    cell: ({ row }) => <TeamCell item={row.original} />,
  },
  {
    id: 'sprint',
    accessorFn: (row) => row.sprint?.name ?? '',
    header: 'Sprint',
    size: 150,
    enableGlobalFilter: true,
    enableColumnFilter: true,
    sortingFn: 'text',
    cell: ({ row }) => <SprintCell item={row.original} />,
  },
  {
    id: 'assignedTo',
    accessorFn: (row) => row.assignedTo?.name ?? '',
    header: 'Assigned To',
    size: 150,
    enableGlobalFilter: true,
    enableColumnFilter: true,
    sortingFn: 'text',
    cell: ({ row }) => <AssignedToCell item={row.original} />,
  },
  // Tags render as overflow-aware chips and filter via the multi-value set
  // panel (individual tags faceted from the displayed rows, tree children
  // included).
  {
    ...createCsvColumn<WorkItemTreeNode>({
      id: 'tags',
      header: 'Tags',
      size: 200,
      getValues: (row) => row.tags ?? [],
    }),
    enableGlobalFilter: true,
  },
  ...(!hideProjectColumn
    ? [
        {
          id: 'project',
          accessorFn: (row: WorkItemTreeNode) => row.project?.name ?? '',
          header: 'Project',
          size: 300,
          enableGlobalFilter: true,
          enableColumnFilter: true,
          sortingFn: 'text' as const,
          cell: ({ row }: { row: any }) => <ProjectCell item={row.original} />,
        } as ColumnDef<WorkItemTreeNode>,
      ]
    : []),
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
