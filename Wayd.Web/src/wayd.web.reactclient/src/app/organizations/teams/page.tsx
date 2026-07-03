'use client'

import PageTitle from '@/src/components/common/page-title'
import {
  WaydGrid2,
  renderTeamLink,
} from '../../../components/common/wayd-grid2'
import { useMemo, useState } from 'react'
import { ItemType } from 'antd/es/menu/interface'
import { Button } from 'antd'
import type { ColumnDef } from '@tanstack/react-table'
import { useDocumentTitle } from '../../../hooks/use-document-title'
import useAuth from '../../../components/contexts/auth'
import { useAppSelector, useAppDispatch } from '../../../hooks'
import { setIncludeInactive } from '../../../store/features/organizations/team-slice'
import { useGetTeamsQuery } from '../../../store/features/organizations/team-api'
import { ModalCreateTeamForm } from '../_components/create-team-form'
import { TeamListItem } from '../types'
import {
  ControlItemsMenu,
  ControlItemSwitch,
} from '../../../components/common/control-items-menu'
import { authorizePage } from '../../../components/hoc'

const TeamListPage = () => {
  useDocumentTitle('Teams')
  const includeInactive = useAppSelector((state) => state.team.includeInactive)
  const {
    data: teams,
    isLoading,
    error,
    refetch,
  } = useGetTeamsQuery(includeInactive)
  const [isCreateOpen, setIsCreateOpen] = useState(false)

  const columns = useMemo<ColumnDef<TeamListItem, any>[]>(
    () => [
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        size: 200,
        meta: { filterEnableSet: true },
        cell: ({ row }) => renderTeamLink(row.original),
      },
      { id: 'code', accessorKey: 'code', header: 'Code', size: 125 },
      {
        id: 'type',
        accessorKey: 'type',
        header: 'Type',
        meta: { filterType: 'set' },
      },
      {
        id: 'teamOfTeams',
        accessorKey: 'teamOfTeams.name',
        header: 'Team of Teams',
        size: 200,
        meta: { filterEnableSet: true },
        cell: ({ row }) => renderTeamLink(row.original.teamOfTeams),
      },
      {
        id: 'isActive',
        accessorKey: 'isActive',
        header: 'Active',
        meta: { columnType: 'yesNo' },
      },
    ],
    [],
  )

  const dispatch = useAppDispatch()

  const { hasPermissionClaim } = useAuth()
  const canCreateTeam = hasPermissionClaim('Permissions.Teams.Create')
  const showActions = canCreateTeam

  const actions = () => {
    if (!showActions) return null
    return (
      <>
        {canCreateTeam && (
          <Button onClick={() => setIsCreateOpen(true)}>
            Create Team
          </Button>
        )}
      </>
    )
  }

  const onIncludeInactiveChange = (checked: boolean) => {
    dispatch(setIncludeInactive(checked))
  }

  const controlItems: ItemType[] = [
    {
      label: (
        <ControlItemSwitch
          label="Include Inactive"
          checked={includeInactive}
          onChange={onIncludeInactiveChange}
        />
      ),
      key: 'include-inactive',
      onClick: () => onIncludeInactiveChange(!includeInactive),
    },
  ]

  return (
    <>
      <PageTitle title="Teams" actions={actions()} />
      <WaydGrid2
        columns={columns}
        data={teams ?? []}
        onRefresh={() => {
          refetch()
        }}
        isLoading={isLoading}
        csvFileName="teams"
        rightSlot={<ControlItemsMenu items={controlItems} />}
      />
      {isCreateOpen && (
        <ModalCreateTeamForm
          open={isCreateOpen}
          onClose={() => setIsCreateOpen(false)}
        />
      )}
    </>
  )
}

const TeamListPageWithAuthorization = authorizePage(
  TeamListPage,
  'Permission',
  'Permissions.Teams.View',
)

export default TeamListPageWithAuthorization
