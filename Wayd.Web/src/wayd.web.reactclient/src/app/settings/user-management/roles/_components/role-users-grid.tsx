'use client'

import { WaydGrid, renderUserLink } from '@/src/components/common/wayd-grid'
import { UserDetailsDto } from '@/src/services/wayd-api'
import { useGetRoleUsersQuery } from '@/src/store/features/user-management/roles-api'
import type { ColumnDef } from '@tanstack/react-table'
import { FC, useMemo } from 'react'

export interface RoleUsersGridProps {
  roleId: string
}

const RoleUsersGrid: FC<RoleUsersGridProps> = (props: RoleUsersGridProps) => {
  const {
    data: usersData,
    isLoading,
    refetch,
  } = useGetRoleUsersQuery(props.roleId)

  const columns = useMemo<ColumnDef<UserDetailsDto, any>[]>(
    () => [
      {
        id: 'userName',
        accessorKey: 'userName',
        header: 'User Name',
        cell: ({ row }) => renderUserLink(row.original),
      },
      { id: 'firstName', accessorKey: 'firstName', header: 'First Name' },
      { id: 'lastName', accessorKey: 'lastName', header: 'Last Name' },
      {
        id: 'roles',
        accessorFn: (row) =>
          row.roles
            ?.map((r) => r.name)
            .sort()
            .join(', ') ?? '',
        header: 'Roles',
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

  const refresh = async () => {
    refetch()
  }

  return (
    <WaydGrid
      columns={columns}
      data={usersData ?? []}
      onRefresh={refresh}
      isLoading={isLoading}
      csvFileName="role-users"
    />
  )
}

export default RoleUsersGrid
