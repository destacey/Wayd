'use client'

import PageTitle from '@/src/components/common/page-title'
import { useEffect, useMemo, useState } from 'react'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import { authorizePage } from '@/src/components/hoc'
import Link from 'next/link'
import { Button } from 'antd'
import useAuth from '@/src/components/contexts/auth'
import { useRouter } from 'next/navigation'
import { useDocumentTitle } from '@/src/hooks'
import { useGetRolesQuery } from '@/src/store/features/user-management/roles-api'
import { CreateRoleForm } from './_components'
import { RoleListDto } from '@/src/services/wayd-api'
import type { ColumnDef } from '@tanstack/react-table'

const RoleListPage = () => {
  useDocumentTitle('Roles')
  const [openCreateRoleForm, setOpenCreateRoleForm] = useState(false)
  const router = useRouter()

  const { data: roleData, isLoading, error, refetch } = useGetRolesQuery()

  const { hasClaim } = useAuth()
  const canCreateRole = hasClaim('Permission', 'Permissions.Roles.Create')

  const columns = useMemo<ColumnDef<RoleListDto, any>[]>(
    () => [
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        cell: ({ row }) => (
          <Link href={`roles/${row.original.id}`}>{row.original.name}</Link>
        ),
      },
      {
        id: 'description',
        accessorKey: 'description',
        header: 'Description',
        size: 300,
      },
    ],
    [],
  )

  useEffect(() => {
    error && console.error(error)
  }, [error])

  const refresh = async () => {
    refetch()
  }

  const actions = () => {
    return (
      <>
        {canCreateRole && (
          <Button onClick={() => setOpenCreateRoleForm(true)}>
            Create Role
          </Button>
        )}
      </>
    )
  }

  return (
    <>
      <PageTitle title="Roles" actions={actions()} />

      <WaydGrid
        columns={columns}
        data={roleData ?? []}
        onRefresh={refresh}
        isLoading={isLoading}
        csvFileName="roles"
      />

      {openCreateRoleForm && (
        <CreateRoleForm
          roles={roleData ?? []}
          onFormCreate={(id: string) => {
            setOpenCreateRoleForm(false)
            router.push(`/settings/user-management/roles/${id}`)
          }}
          onFormCancel={() => setOpenCreateRoleForm(false)}
        />
      )}
    </>
  )
}

const PageWithAuthorization = authorizePage(
  RoleListPage,
  'Permission',
  'Permissions.Roles.View',
)

export default PageWithAuthorization
