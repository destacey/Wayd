'use client'

import {
  TagListCell,
  WaydGrid,
  type GroupHeaderContext,
} from '@/src/components/common/wayd-grid'
import type { ColumnDef, Row } from '@/src/components/common/wayd-grid-core'
import {
  ProjectDeliverableDto,
  ProjectPlanNodeDto,
} from '@/src/services/wayd-api'
import { Flex, Typography } from 'antd'
import { ReactNode } from 'react'
import {
  buildDeliverablePlanRows,
  type DeliverablePlanRow,
} from './deliverable-plan-rows'
import DeliverableProgress from './deliverable-progress'

const { Text } = Typography

export interface ProjectPlanByDeliverableProps {
  projectKey: string
  plan: ProjectPlanNodeDto[]
  deliverables: ProjectDeliverableDto[]
  isLoading: boolean
  onRefresh: () => void
  /** The toolbar's left slot, where the plan's Group by choice sits. */
  leftSlot?: ReactNode
}

/** Deliverables in their own order, with work serving none last. */
const byDeliverableOrder = (
  a: Row<DeliverablePlanRow>,
  b: Row<DeliverablePlanRow>,
) =>
  (a.original.deliverable?.order ?? Number.MAX_SAFE_INTEGER) -
  (b.original.deliverable?.order ?? Number.MAX_SAFE_INTEGER)

const columns: ColumnDef<DeliverablePlanRow, any>[] = [
  {
    // Grouped on, so the groups come out in this column's sort: a group row's
    // original is its first row, which carries the deliverable.
    id: 'deliverable',
    accessorKey: 'deliverableName',
    header: 'Deliverable',
    size: 220,
    sortFn: byDeliverableOrder,
  },
  { id: 'wbs', accessorKey: 'task.wbs', header: '#', size: 80 },
  { id: 'key', accessorKey: 'task.key', header: 'Key', size: 120 },
  { id: 'name', accessorKey: 'task.name', header: 'Name', size: 320 },
  {
    id: 'type',
    accessorKey: 'task.type.name',
    header: 'Type',
    size: 100,
    meta: { filterType: 'set' },
  },
  {
    id: 'status',
    accessorKey: 'task.status.name',
    header: 'Status',
    size: 120,
    meta: { filterType: 'set' },
  },
  {
    id: 'progress',
    accessorFn: (row) => row.task.progress,
    header: 'Progress',
    size: 90,
    cell: ({ row }) => `${row.original.task.progress}%`,
  },
  {
    id: 'start',
    accessorFn: (row) => row.task.start ?? row.task.plannedDate,
    header: 'Start',
    size: 120,
    meta: { columnType: 'dateOnly' },
  },
  {
    id: 'end',
    accessorFn: (row) => row.task.end ?? row.task.plannedDate,
    header: 'End',
    size: 120,
    meta: { columnType: 'dateOnly' },
  },
  {
    id: 'assignees',
    accessorFn: (row) =>
      row.task.assignees?.map((a) => a.name).join(', ') ?? '',
    header: 'Assignees',
    size: 200,
    cell: ({ row }) => {
      const names = row.original.task.assignees?.map((a) => a.name) ?? []
      return names.length === 0 ? '' : <TagListCell values={names} />
    },
  },
]

const renderGroupHeader = ({
  value,
  leafRows,
}: GroupHeaderContext<DeliverablePlanRow>) => {
  const deliverable = leafRows[0]?.deliverable
  return (
    <Flex gap="middle" align="center" wrap>
      <span>{String(value)}</span>
      <Text type="secondary">
        {leafRows.length} {leafRows.length === 1 ? 'task' : 'tasks'}
      </Text>
      {deliverable && <DeliverableProgress progress={deliverable.progress} />}
    </Flex>
  )
}

/**
 * The plan regrouped by the deliverables it serves, for reading what is left
 * to do for each. Read-only: links are changed in a task's form or from the
 * Deliverables section, and the plan's own grid stays the place to edit
 * tasks. Each heading shows the deliverable's derived progress, which counts
 * a linked task's subtasks, so the rows beneath are the linked tasks only.
 */
const ProjectPlanByDeliverable = ({
  projectKey,
  plan,
  deliverables,
  isLoading,
  onRefresh,
  leftSlot,
}: ProjectPlanByDeliverableProps) => (
  <WaydGrid<DeliverablePlanRow>
    data={buildDeliverablePlanRows(plan, deliverables)}
    columns={columns}
    isLoading={isLoading}
    getRowId={(row) => row.rowId}
    grouping={['deliverable']}
    renderGroupHeader={renderGroupHeader}
    onRefresh={async () => onRefresh()}
    leftSlot={leftSlot}
    persistStateKey="ppm-project-plan-by-deliverable"
    csvFileName={`${projectKey}-plan-by-deliverable`}
    emptyMessage="No plan found"
  />
)

export default ProjectPlanByDeliverable
