'use client'

import { useState, useMemo } from 'react'
import {
  WaydGrid,
  createActionsColumn,
  createMultiValueSetFilter,
} from '@/src/components/common/wayd-grid'
import useAuth from '@/src/components/contexts/auth'
import { ItemType } from 'antd/es/menu/interface'
import { Flex, Tag, Tooltip } from 'antd'
import type { ColumnDef } from '@tanstack/react-table'
import Link from 'next/link'
import {
  TeamMemberDto,
  useGetTeamMembersQuery,
  useGetTeamOfTeamsMembersQuery,
} from '@/src/store/features/organization/team-members-api'
import { useGetTeamMemberRolesQuery } from '@/src/store/features/organization/team-member-roles-api'
import EditTeamMemberForm from './edit-team-member-form'
import RemoveTeamMemberForm from './remove-team-member-form'

interface TeamMembersGridProps {
  teamId: string
  teamType: 'Team' | 'TeamOfTeams'
}

/** A member's roles as their display names (source for the cell, the Roles set
 *  filter, and CSV export). */
const roleNames = (member: TeamMemberDto): string[] =>
  member.roles.map((r) => r.name)

const rolesFilter = createMultiValueSetFilter<TeamMemberDto>(roleNames)

const TeamMembersGrid = ({ teamId, teamType }: TeamMembersGridProps) => {
  const [editingMember, setEditingMember] = useState<TeamMemberDto | null>(null)
  const [removingMember, setRemovingMember] = useState<TeamMemberDto | null>(
    null,
  )

  const { hasPermissionClaim } = useAuth()
  const canUpdate = hasPermissionClaim('Permissions.Teams.Update')

  const { data: allRoles } = useGetTeamMemberRolesQuery(false)
  const roleDescriptionById = useMemo(() => {
    const map = new Map<string, string | undefined>()
    allRoles?.forEach((r) => map.set(r.id, r.description ?? undefined))
    return map
  }, [allRoles])

  const {
    data: teamMembers,
    isLoading: teamLoading,
    refetch: refetchTeam,
  } = useGetTeamMembersQuery(
    { teamId },
    { skip: !teamId || teamType !== 'Team' },
  )
  const {
    data: totMembers,
    isLoading: totLoading,
    refetch: refetchTot,
  } = useGetTeamOfTeamsMembersQuery(
    { teamId },
    { skip: !teamId || teamType !== 'TeamOfTeams' },
  )

  const members = teamType === 'Team' ? teamMembers : totMembers
  const isLoading = teamType === 'Team' ? teamLoading : totLoading
  const refetch = teamType === 'Team' ? refetchTeam : refetchTot

  // Distinct role names across the visible members, for the set filter's
  // checkbox list (individual roles, not whole combinations).
  const roleFilterOptions = useMemo(() => {
    const names = new Set<string>()
    members?.forEach((m) => m.roles.forEach((r) => names.add(r.name)))
    return Array.from(names)
      .sort()
      .map((name) => ({ label: name, value: name }))
  }, [members])

  const columns = useMemo<ColumnDef<TeamMemberDto, any>[]>(() => {
    return [
      createActionsColumn<TeamMemberDto>({
        hide: !canUpdate,
        ariaLabel: 'Team member actions',
        getItems: (member): ItemType[] => {
          if (!canUpdate) return []
          return [
            {
              key: 'edit',
              label: 'Edit',
              onClick: () => setEditingMember(member),
            },
            {
              key: 'remove',
              label: 'Remove',
              danger: true,
              onClick: () => setRemovingMember(member),
            },
          ]
        },
      }),
      {
        id: 'name',
        accessorKey: 'employee.name',
        header: 'Name',
        size: 200,
        meta: { filterEnableSet: true },
        cell: ({ row }) => (
          <Link href={`/organizations/employees/${row.original.employee.key}`}>
            {row.original.employee.name}
          </Link>
        ),
      },
      {
        id: 'jobTitle',
        accessorKey: 'employee.jobTitle',
        header: 'Title',
        size: 300,
      },
      {
        id: 'email',
        accessorKey: 'employee.email',
        header: 'Email',
        size: 300,
      },
      {
        id: 'roles',
        accessorFn: (row) => roleNames(row).join(', '),
        header: 'Roles',
        size: 400,
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
    ]
  }, [canUpdate, roleDescriptionById, roleFilterOptions])

  return (
    <>
      <WaydGrid
        columns={columns}
        data={members ?? []}
        isLoading={isLoading}
        onRefresh={() => {
          refetch()
        }}
        csvFileName="team-members"
      />
      {editingMember && (
        <EditTeamMemberForm
          teamId={teamId}
          teamType={teamType}
          member={editingMember}
          onFormComplete={() => {
            setEditingMember(null)
            refetch()
          }}
          onFormCancel={() => setEditingMember(null)}
        />
      )}
      {removingMember && (
        <RemoveTeamMemberForm
          teamId={teamId}
          teamType={teamType}
          member={removingMember}
          onFormComplete={() => {
            setRemovingMember(null)
            refetch()
          }}
          onFormCancel={() => setRemovingMember(null)}
        />
      )}
    </>
  )
}

export default TeamMembersGrid
