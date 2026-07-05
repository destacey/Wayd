'use client'

import { useState, useMemo } from 'react'
import {
  WaydGrid,
  createActionsColumn,
  renderTeamLink,
} from '../../../components/common/wayd-grid'
import { TeamMembershipDto } from '@/src/services/wayd-api'
import useAuth from '../../../components/contexts/auth'
import { ItemType } from 'antd/es/menu/interface'
import EditTeamMembershipForm from './edit-team-membership-form'
import { TeamTypeName } from '../types'
import DeleteTeamMembershipForm from './delete-team-membership-form'
import type { ColumnDef } from '@tanstack/react-table'

export interface TeamMembershipsGridProps {
  teamId: string
  teamMemberships: TeamMembershipDto[] | undefined
  isLoading: boolean
  refetch: () => void
  teamType: TeamTypeName
}

const TeamMembershipsGrid = ({
  teamId,
  teamMemberships,
  isLoading,
  refetch,
  teamType,
}: TeamMembershipsGridProps) => {
  const [openEditTeamMembershipForm, setOpenEditTeamMembershipForm] =
    useState<boolean>(false)
  const [openDeleteTeamMembershipForm, setOpenDeleteTeamMembershipForm] =
    useState<boolean>(false)
  const [selectedTeamMembership, setSelectedTeamMembership] =
    useState<TeamMembershipDto | null>(null)
  const { hasClaim } = useAuth()
  const canManageTeamMemberships = hasClaim(
    'Permission',
    'Permissions.Teams.ManageTeamMemberships',
  )
  const showRowActions = canManageTeamMemberships

  const refresh = async () => {
    refetch()
  }

  const columns = useMemo<ColumnDef<TeamMembershipDto, any>[]>(() => {
    const onEditTeamMembershipMenuClicked = (membership: TeamMembershipDto) => {
      setSelectedTeamMembership(membership)
      setOpenEditTeamMembershipForm(true)
    }

    const onDeleteTeamMembershipMenuClicked = (
      membership: TeamMembershipDto,
    ) => {
      setSelectedTeamMembership(membership)
      setOpenDeleteTeamMembershipForm(true)
    }

    return [
      createActionsColumn<TeamMembershipDto>({
        hide: !showRowActions,
        ariaLabel: 'Team membership actions',
        getItems: (membership): ItemType[] => {
          // only allow editing memberships for the current team
          if (teamId != membership.child.id) return []

          return [
            {
              key: 'edit-team-membership',
              label: 'Edit Team Membership',
              disabled: !canManageTeamMemberships,
              onClick: () => onEditTeamMembershipMenuClicked(membership),
            },
            {
              key: 'delete-team-membership',
              label: 'Delete Team Membership',
              disabled: !canManageTeamMemberships,
              onClick: () => onDeleteTeamMembershipMenuClicked(membership),
            },
          ]
        },
      }),
      {
        id: 'child',
        accessorKey: 'child.name',
        header: 'Child Team',
        size: 200,
        meta: { filterEnableSet: true },
        cell: ({ row }) => renderTeamLink(row.original.child),
      },
      {
        id: 'parent',
        accessorKey: 'parent.name',
        header: 'Parent Team',
        size: 200,
        meta: { filterEnableSet: true },
        cell: ({ row }) => renderTeamLink(row.original.parent),
      },
      {
        id: 'state',
        accessorKey: 'state',
        header: 'State',
        meta: { filterType: 'set' },
      },
      {
        id: 'start',
        accessorKey: 'start',
        header: 'Start',
        meta: { columnType: 'dateOnly' },
      },
      {
        id: 'end',
        accessorKey: 'end',
        header: 'End',
        meta: { columnType: 'dateOnly' },
      },
    ]
  }, [showRowActions, teamId, canManageTeamMemberships])

  const onEditTeamMembershipFormClosed = (wasCreated: boolean) => {
    setOpenEditTeamMembershipForm(false)
    setSelectedTeamMembership(null)
    if (wasCreated) {
      refresh()
    }
  }

  const onDeleteTeamMembershipFormClosed = (wasCreated: boolean) => {
    setOpenDeleteTeamMembershipForm(false)
    setSelectedTeamMembership(null)
    if (wasCreated) {
      refresh()
    }
  }
  return (
    <>
      <WaydGrid
        columns={columns}
        data={teamMemberships ?? []}
        isLoading={isLoading}
        onRefresh={refresh}
        csvFileName="team-memberships"
        persistStateKey={
          teamType === 'Team'
            ? 'team-memberships'
            : 'team-of-teams-memberships'
        }
      />
      {openEditTeamMembershipForm && selectedTeamMembership && (
        <EditTeamMembershipForm
          membership={selectedTeamMembership}
          teamType={teamType}
          onFormSave={() => onEditTeamMembershipFormClosed(true)}
          onFormCancel={() => onEditTeamMembershipFormClosed(false)}
        />
      )}
      {openDeleteTeamMembershipForm && selectedTeamMembership && (
        <DeleteTeamMembershipForm
          membership={selectedTeamMembership}
          teamType={teamType}
          onFormSave={() => onDeleteTeamMembershipFormClosed(true)}
          onFormCancel={() => onDeleteTeamMembershipFormClosed(false)}
        />
      )}
    </>
  )
}

export default TeamMembershipsGrid
