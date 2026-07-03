'use client'

import { useMemo } from 'react'
import {
  WaydGrid2,
  renderTeamLink,
  createMultiValueSetFilter,
} from '@/src/components/common/wayd-grid2'
import type { ColumnDef } from '@tanstack/react-table'
import { Flex, Tag, Tooltip } from 'antd'
import {
  TeamMemberDto,
  useGetEmployeeTeamMembershipsQuery,
} from '@/src/store/features/organization/team-members-api'
import { useGetTeamMemberRolesQuery } from '@/src/store/features/organization/team-member-roles-api'

interface Props {
  employeeId: string
}

/** Membership roles as their display names (the multi-value source for the cell,
 *  the Roles set filter, and CSV export). */
const roleNames = (member: TeamMemberDto): string[] =>
  member.roles.map((r) => r.name)

const rolesFilter = createMultiValueSetFilter<TeamMemberDto>(roleNames)

const EmployeeTeamsGrid = ({ employeeId }: Props) => {
  const {
    data: memberships,
    isLoading,
    refetch,
  } = useGetEmployeeTeamMembershipsQuery({ employeeId }, { skip: !employeeId })

  const { data: allRoles } = useGetTeamMemberRolesQuery(false)
  const roleDescriptionById = useMemo(() => {
    const map = new Map<string, string | undefined>()
    allRoles?.forEach((r) => map.set(r.id, r.description ?? undefined))
    return map
  }, [allRoles])

  // Distinct role names present in this employee's memberships, for the set
  // filter's checkbox list (individual roles, not whole combinations).
  const roleFilterOptions = useMemo(() => {
    const names = new Set<string>()
    memberships?.forEach((m) => m.roles.forEach((r) => names.add(r.name)))
    return Array.from(names)
      .sort()
      .map((name) => ({ label: name, value: name }))
  }, [memberships])

  const columns = useMemo<ColumnDef<TeamMemberDto, any>[]>(
    () => [
      {
        id: 'team',
        accessorKey: 'team.name',
        header: 'Team',
        size: 200,
        meta: { filterEnableSet: true },
        cell: ({ row }) => renderTeamLink(row.original.team),
      },
      {
        id: 'type',
        accessorKey: 'team.type',
        header: 'Type',
        size: 150,
        meta: { filterType: 'set' },
      },
      {
        id: 'roles',
        accessorFn: (row) => roleNames(row).join(', '),
        header: 'Roles',
        size: 250,
        filterFn: rolesFilter,
        meta: { filterEnableSet: true, filterOptions: roleFilterOptions },
        cell: ({ row }) => (
          <Flex wrap gap={4}>
            {row.original.roles.map((role) => (
              <Tooltip
                key={role.id}
                title={roleDescriptionById.get(role.id)}
                placement="top"
              >
                <Tag variant="filled">{role.name}</Tag>
              </Tooltip>
            ))}
          </Flex>
        ),
      },
    ],
    [roleDescriptionById, roleFilterOptions],
  )

  return (
    <WaydGrid2
      columns={columns}
      data={memberships ?? []}
      isLoading={isLoading}
      onRefresh={() => {
        refetch()
      }}
      csvFileName="employee-teams"
    />
  )
}

export default EmployeeTeamsGrid
