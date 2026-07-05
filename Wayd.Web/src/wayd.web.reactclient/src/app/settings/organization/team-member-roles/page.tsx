'use client'

import { PageTitle } from '@/src/components/common'
import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { TeamMemberRoleDto } from '@/src/services/wayd-api'
import {
  useGetTeamMemberRolesQuery,
  useActivateTeamMemberRoleMutation,
  useDeactivateTeamMemberRoleMutation,
} from '@/src/store/features/organization/team-member-roles-api'
import type { ColumnDef } from '@tanstack/react-table'
import { Button } from 'antd'
import { useEffect, useMemo, useState } from 'react'
import {
  ControlItemsMenu,
  ControlItemSwitch,
} from '@/src/components/common/control-items-menu'
import { ItemType } from 'antd/es/menu/interface'
import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError } from '@/src/utils'
import CreateTeamMemberRoleForm from './_components/create-team-member-role-form'
import EditTeamMemberRoleForm from './_components/edit-team-member-role-form'
import DeleteTeamMemberRoleForm from './_components/delete-team-member-role-form'

const TeamMemberRolesPage = () => {
  useDocumentTitle('Organization - Team Member Roles')

  const [includeInactive, setIncludeInactive] = useState(false)
  const [openCreateForm, setOpenCreateForm] = useState(false)
  const [editingRole, setEditingRole] = useState<TeamMemberRoleDto | null>(null)
  const [deletingRole, setDeletingRole] = useState<TeamMemberRoleDto | null>(
    null,
  )

  const messageApi = useMessage()
  const { hasPermissionClaim } = useAuth()

  const canCreate = hasPermissionClaim('Permissions.TeamMemberRoles.Create')
  const canUpdate = hasPermissionClaim('Permissions.TeamMemberRoles.Update')
  const canDelete = hasPermissionClaim('Permissions.TeamMemberRoles.Delete')

  const {
    data: roles,
    isLoading,
    error,
    refetch,
  } = useGetTeamMemberRolesQuery(includeInactive)
  const [activateRole] = useActivateTeamMemberRoleMutation()
  const [deactivateRole] = useDeactivateTeamMemberRoleMutation()

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading team member roles.',
      )
      console.error(error)
    }
  }, [error, messageApi])

  const columns = useMemo<ColumnDef<TeamMemberRoleDto, any>[]>(() => {
    const getRowMenuItems = (role: TeamMemberRoleDto): ItemType[] => {
      const items: ItemType[] = []
      if (canUpdate) {
        items.push({
          key: 'edit',
          label: 'Edit',
          onClick: () => setEditingRole(role),
        })
        if (role.isActive) {
          items.push({
            key: 'deactivate',
            label: 'Deactivate',
            onClick: async () => {
              const response = await deactivateRole(role.id)
              if (response.error) {
                messageApi.error(
                  (isApiError(response.error)
                    ? response.error.detail
                    : undefined) ?? 'Failed to deactivate role.',
                )
              } else {
                messageApi.success(`"${role.name}" deactivated.`)
              }
            },
          })
        } else {
          items.push({
            key: 'activate',
            label: 'Activate',
            onClick: async () => {
              const response = await activateRole(role.id)
              if (response.error) {
                messageApi.error(
                  (isApiError(response.error)
                    ? response.error.detail
                    : undefined) ?? 'Failed to activate role.',
                )
              } else {
                messageApi.success(`"${role.name}" activated.`)
              }
            },
          })
        }
      }
      if (canDelete) {
        items.push({
          key: 'delete',
          label: 'Delete',
          danger: true,
          onClick: () => setDeletingRole(role),
        })
      }
      return items
    }

    return [
      createActionsColumn<TeamMemberRoleDto>({
        hide: !canUpdate && !canDelete,
        ariaLabel: 'Team member role actions',
        getItems: (role) => getRowMenuItems(role),
      }),
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      { id: 'name', accessorKey: 'name', header: 'Name', size: 250 },
      {
        id: 'description',
        accessorKey: 'description',
        header: 'Description',
        size: 400,
      },
      {
        id: 'isActive',
        accessorKey: 'isActive',
        header: 'Active',
        meta: { columnType: 'yesNo' },
      },
    ]
  }, [canUpdate, canDelete, activateRole, deactivateRole, messageApi])

  const controlItems: ItemType[] = [
    {
      label: (
        <ControlItemSwitch
          label="Include Inactive"
          checked={includeInactive}
          onChange={(checked) => setIncludeInactive(checked)}
        />
      ),
      key: 'include-inactive',
      onClick: () => setIncludeInactive((prev) => !prev),
    },
  ]

  const actions = canCreate ? (
    <Button onClick={() => setOpenCreateForm(true)}>
      Create Team Member Role
    </Button>
  ) : null

  return (
    <>
      <PageTitle title="Team Member Roles" actions={actions} />

      <WaydGrid
        columns={columns}
        data={roles ?? []}
        onRefresh={() => {
          refetch()
        }}
        isLoading={isLoading}
        persistStateKey="settings-team-member-roles"
        csvFileName="team-member-roles"
        rightSlot={<ControlItemsMenu items={controlItems} />}
      />

      {openCreateForm && (
        <CreateTeamMemberRoleForm
          onFormComplete={() => {
            setOpenCreateForm(false)
            refetch()
          }}
          onFormCancel={() => setOpenCreateForm(false)}
        />
      )}
      {editingRole && (
        <EditTeamMemberRoleForm
          role={editingRole}
          onFormComplete={() => {
            setEditingRole(null)
            refetch()
          }}
          onFormCancel={() => setEditingRole(null)}
        />
      )}
      {deletingRole && (
        <DeleteTeamMemberRoleForm
          role={deletingRole}
          onFormComplete={() => {
            setDeletingRole(null)
            refetch()
          }}
          onFormCancel={() => setDeletingRole(null)}
        />
      )}
    </>
  )
}

const TeamMemberRolesPageWithAuthorization = authorizePage(
  TeamMemberRolesPage,
  'Permission',
  'Permissions.TeamMemberRoles.View',
)

export default TeamMemberRolesPageWithAuthorization
