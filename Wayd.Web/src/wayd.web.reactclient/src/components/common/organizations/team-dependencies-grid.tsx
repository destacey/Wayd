'use client'

import { DependencyDto, TeamDetailsDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@tanstack/react-table'
import { FC, useMemo } from 'react'
import {
  WaydGrid,
  renderDependencyHealthTag,
  renderSprintLink,
  renderTeamLink,
  renderWorkItemLink,
  workItemKeySort,
} from '../wayd-grid'
import { DEPENDENCY_SCOPE_TOOLTIP } from '../work/dependency-constants'

export interface TeamDependenciesGridProps {
  team: TeamDetailsDto
  dependencies: DependencyDto[]
  isLoading: boolean
  refetch: () => void
}

const TeamDependenciesGrid: FC<TeamDependenciesGridProps> = (props) => {
  const { refetch } = props

  const columns = useMemo<ColumnDef<DependencyDto, any>[]>(
    () => [
      {
        id: 'dependencyInfo',
        header: 'Dependency Info',
        columns: [
          {
            id: 'state',
            accessorKey: 'state.name',
            header: 'State',
            size: 125,
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
            meta: { filterType: 'set', headerTooltip: DEPENDENCY_SCOPE_TOOLTIP },
          },
        ],
      },
      {
        id: 'predecessorInfo',
        header: 'Predecessor Info',
        columns: [
          {
            id: 'sourceKey',
            accessorKey: 'source.key',
            header: 'Key',
            sortingFn: workItemKeySort,
            cell: ({ row }) => renderWorkItemLink(row.original.source),
          },
          {
            id: 'sourceTitle',
            accessorKey: 'source.title',
            header: 'Title',
            size: 400,
          },
          {
            id: 'sourceType',
            accessorKey: 'source.type',
            header: 'Type',
            size: 150,
            meta: { filterType: 'set' },
          },
          {
            id: 'sourceStatus',
            accessorKey: 'source.status',
            header: 'Status',
            size: 150,
            meta: { filterType: 'set' },
          },
          {
            id: 'sourceTeam',
            accessorKey: 'source.team.name',
            header: 'Team',
            meta: { filterEnableSet: true },
            cell: ({ row }) => renderTeamLink(row.original.source.team),
          },
          {
            id: 'sourceSprint',
            accessorKey: 'source.sprint.name',
            header: 'Sprint',
            meta: { filterEnableSet: true },
            cell: ({ row }) =>
              renderSprintLink(row.original.source.sprint, {
                showTeamCode: false,
              }),
          },
        ],
      },
      {
        id: 'successorInfo',
        header: 'Successor Info',
        columns: [
          {
            id: 'targetKey',
            accessorKey: 'target.key',
            header: 'Key',
            sortingFn: workItemKeySort,
            cell: ({ row }) => renderWorkItemLink(row.original.target),
          },
          {
            id: 'targetTitle',
            accessorKey: 'target.title',
            header: 'Title',
            size: 400,
          },
          {
            id: 'targetType',
            accessorKey: 'target.type',
            header: 'Type',
            size: 150,
            meta: { filterType: 'set' },
          },
          {
            id: 'targetStatus',
            accessorKey: 'target.status',
            header: 'Status',
            size: 150,
            meta: { filterType: 'set' },
          },
          {
            id: 'targetTeam',
            accessorKey: 'target.team.name',
            header: 'Team',
            meta: { filterEnableSet: true },
            cell: ({ row }) => renderTeamLink(row.original.target.team),
          },
          {
            id: 'targetSprint',
            accessorKey: 'target.sprint.name',
            header: 'Sprint',
            meta: { filterEnableSet: true },
            cell: ({ row }) =>
              renderSprintLink(row.original.target.sprint, {
                showTeamCode: false,
              }),
          },
        ],
      },
    ],
    [],
  )

  const refresh = async () => {
    refetch()
  }

  return (
    <WaydGrid
      height={550}
      columns={columns}
      data={props.dependencies}
      onRefresh={refresh}
      isLoading={props.isLoading}
      csvFileName="team-dependencies"
    />
  )
}

export default TeamDependenciesGrid
