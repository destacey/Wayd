'use client'

import { useMemo } from 'react'
import {
  WaydGrid,
  caseInsensitiveCompare,
  renderTeamLink,
  createMultiValueSetFilter,
} from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { Flex, Tag, Tooltip } from 'antd'
import {
  TeamMemberDto,
  useGetEmployeeTeamMembershipsQuery,
} from '@/src/store/features/organization/team-members-api'
import { useGetTeamMemberRolesQuery } from '@/src/store/features/organization/team-member-roles-api'
import { useGetEmployeeAllocationsQuery } from '@/src/store/features/organization/employee-allocations-api'
import { useFeatureFlag } from '@/src/hooks'
import { todayCalendarDate } from '@/src/utils'
import { allocatedOn } from './allocation-periods'

interface Props {
  employeeId: string
}

/** Membership roles as their display names (the multi-value source for the cell,
 *  the Roles set filter, and CSV export). */
const roleNames = (member: TeamMemberDto): string[] =>
  member.roles.map((r) => r.name)

/** A membership with the share of the person's time its team has today. */
type TeamRow = TeamMemberDto & { allocation: number }

const rolesFilter = createMultiValueSetFilter<TeamRow>(roleNames)

const EmployeeTeamsGrid = ({ employeeId }: Props) => {
  const {
    data: memberships,
    isLoading,
    refetch,
  } = useGetEmployeeTeamMembershipsQuery({ employeeId }, { skip: !employeeId })

  const { data: allRoles } = useGetTeamMemberRolesQuery(false)

  const { isEnabled: capacityEnabled } = useFeatureFlag(
    'team-capacity-management',
  )
  const { data: allocations } = useGetEmployeeAllocationsQuery(
    { employeeId },
    { skip: !employeeId || !capacityEnabled },
  )
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
      .sort(caseInsensitiveCompare)
      .map((name) => ({ label: name, value: name }))
  }, [memberships])

  // The allocation is part of each row, not read by the column from the
  // allocations query: the grid keeps a row's cell values until its data
  // changes, so a column reading a query that loads later would stay at 0%.
  const rows = useMemo<TeamRow[]>(() => {
    const today = todayCalendarDate()
    return (memberships ?? []).map((membership) => ({
      ...membership,
      allocation: allocatedOn(
        (allocations ?? []).filter((a) => a.team.id === membership.team.id),
        today,
      ),
    }))
  }, [memberships, allocations])

  const columns = useMemo<ColumnDef<TeamRow, any>[]>(
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
      ...(capacityEnabled
        ? [
            {
              id: 'allocation',
              accessorKey: 'allocation',
              header: 'Allocation',
              size: 120,
              cell: ({ getValue }: { getValue: () => number }) =>
                `${getValue()}%`,
            },
          ]
        : []),
    ],
    [roleDescriptionById, roleFilterOptions, capacityEnabled],
  )

  return (
    <WaydGrid
      columns={columns}
      data={rows}
      isLoading={isLoading}
      onRefresh={() => {
        refetch()
      }}
      persistStateKey="employee-teams"
      csvFileName="employee-teams"
    />
  )
}

export default EmployeeTeamsGrid
