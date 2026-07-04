'use client'

import { WaydGrid } from '@/src/components/common/wayd-grid'
import { ProjectTeamMemberDto } from '@/src/services/wayd-api'
import { useGetProjectTeamQuery } from '@/src/store/features/ppm/projects-api'
import type { ColumnDef } from '@tanstack/react-table'
import Link from 'next/link'
import { FC } from 'react'

const columns: ColumnDef<ProjectTeamMemberDto, any>[] = [
  {
    id: 'person',
    accessorKey: 'employee.name',
    header: 'Person',
    size: 250,
    cell: ({ row }) => (
      <Link
        href={`/organizations/employees/${row.original.employee.key}`}
        prefetch={false}
      >
        {row.original.employee.name}
      </Link>
    ),
  },
  {
    id: 'roles',
    accessorFn: (row) => row.roles?.join(', ') ?? '',
    header: 'Roles',
    size: 250,
  },
  {
    id: 'assignedPhases',
    accessorFn: (row) => row.assignedPhases?.join(', ') || '',
    header: 'Assigned Phases',
    size: 250,
  },
  {
    id: 'activeWorkItemCount',
    accessorKey: 'activeWorkItemCount',
    header: 'Active Tasks',
    size: 130,
  },
]

interface ProjectTeamGridProps {
  projectIdOrKey: string
}

const ProjectTeamGrid: FC<ProjectTeamGridProps> = ({ projectIdOrKey }) => {
  const {
    data: teamData,
    isLoading,
    refetch,
  } = useGetProjectTeamQuery(projectIdOrKey)

  const refresh = async () => {
    refetch()
  }

  return (
    <WaydGrid
      columns={columns}
      data={teamData ?? []}
      onRefresh={refresh}
      isLoading={isLoading}
      csvFileName="project-team"
      emptyMessage="No team members assigned."
    />
  )
}

export default ProjectTeamGrid
