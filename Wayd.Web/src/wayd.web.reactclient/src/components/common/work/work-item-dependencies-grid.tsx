'use client'

import {
  ScopedDependencyDto,
  WorkItemDetailsDto,
} from '@/src/services/wayd-api'
import type { ColumnDef } from '@tanstack/react-table'
import { FC, useMemo } from 'react'
import { WaydTooltip } from '@/src/components/common'
import {
  WaydGrid,
  renderDependencyHealthTag,
  renderSprintLink,
  renderTeamLink,
  renderWorkItemLink,
  workItemKeySort,
} from '../wayd-grid'
import { DEPENDENCY_SCOPE_TOOLTIP } from './dependency-constants'

export interface WorkItemDependenciesGridProps {
  workItem: WorkItemDetailsDto
  dependencies: ScopedDependencyDto[]
  isLoading: boolean
  refetch: () => void
}

const dependencyTypeTooltip = (
  data: ScopedDependencyDto,
  workItem: WorkItemDetailsDto,
) => {
  if (data.type === 'Successor') {
    return `${data.dependency.key} is a successor to ${workItem.key}.  This means that ${data.dependency.key} cannot be completed until ${workItem.key} is completed.`
  } else if (data.type === 'Predecessor') {
    return `${data.dependency.key} is a predecessor to ${workItem.key}. This means that ${workItem.key} cannot be completed until ${data.dependency.key} is completed.`
  }

  return 'Unknown dependency type'
}

const WorkItemDependenciesGrid: FC<WorkItemDependenciesGridProps> = (props) => {
  const { workItem, refetch } = props

  const columns = useMemo<ColumnDef<ScopedDependencyDto, any>[]>(
    () => [
      {
        id: 'dependencyInfo',
        header: 'Dependency Info',
        columns: [
          {
            id: 'type',
            accessorKey: 'type',
            header: 'Type',
            size: 125,
            meta: { filterType: 'set' },
            cell: ({ row, getValue }) => (
              <WaydTooltip
                title={dependencyTypeTooltip(row.original, workItem)}
              >
                <span>{getValue<string>()}</span>
              </WaydTooltip>
            ),
          },
          {
            id: 'state',
            accessorKey: 'state.name',
            header: 'State',
            size: 100,
            meta: { filterType: 'set' },
          },
          {
            id: 'health',
            accessorKey: 'health.name',
            header: 'Health',
            size: 100,
            meta: { filterType: 'set' },
            cell: ({ row }) => renderDependencyHealthTag(row.original.health),
          },
          {
            id: 'scope',
            accessorKey: 'scope.name',
            header: 'Scope',
            size: 100,
            meta: {
              filterType: 'set',
              headerTooltip: DEPENDENCY_SCOPE_TOOLTIP,
            },
          },
        ],
      },
      {
        id: 'workItemInfo',
        header: 'Work Item Info',
        columns: [
          {
            id: 'key',
            accessorKey: 'dependency.key',
            header: 'Key',
            sortingFn: workItemKeySort,
            cell: ({ row }) => renderWorkItemLink(row.original.dependency),
          },
          {
            id: 'title',
            accessorKey: 'dependency.title',
            header: 'Title',
            size: 400,
          },
          {
            id: 'workItemType',
            accessorKey: 'dependency.type',
            header: 'Type',
            size: 150,
            meta: { filterType: 'set' },
          },
          {
            id: 'status',
            accessorKey: 'dependency.status',
            header: 'Status',
            size: 150,
            meta: { filterType: 'set' },
          },
          {
            id: 'team',
            accessorKey: 'dependency.team.name',
            header: 'Team',
            meta: { filterEnableSet: true },
            cell: ({ row }) => renderTeamLink(row.original.dependency.team),
          },
          {
            id: 'sprint',
            accessorKey: 'dependency.sprint.name',
            header: 'Sprint',
            meta: { filterEnableSet: true },
            cell: ({ row }) =>
              renderSprintLink(row.original.dependency.sprint, {
                showTeamCode: false,
              }),
          },
        ],
      },
    ],
    [workItem],
  )

  const refresh = async () => {
    refetch()
  }

  return (
    <WaydGrid
      columns={columns}
      data={props.dependencies}
      onRefresh={refresh}
      isLoading={props.isLoading}
      initialSorting={[{ id: 'type', desc: false }]}
      csvFileName="work-item-dependencies"
    />
  )
}

export default WorkItemDependenciesGrid
