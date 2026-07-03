'use client'

import { PageTitle } from '@/src/components/common'
import { WaydGrid2 } from '@/src/components/common/wayd-grid2'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { ProjectLifecycleListDto } from '@/src/services/wayd-api'
import { useGetProjectLifecyclesQuery } from '@/src/store/features/ppm/project-lifecycles-api'
import type { ColumnDef } from '@tanstack/react-table'
import { Button } from 'antd'
import Link from 'next/link'
import { useEffect, useMemo, useState } from 'react'
import CreateProjectLifecycleForm from './_components/create-project-lifecycle-form'
import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError } from '@/src/utils'

const ProjectLifecyclesPage = () => {
  useDocumentTitle('PPM - Project Lifecycles')
  const [openCreateForm, setOpenCreateForm] = useState<boolean>(false)

  const messageApi = useMessage()

  const {
    data: lifecycleData,
    isLoading,
    error,
    refetch,
  } = useGetProjectLifecyclesQuery(null)

  const { hasPermissionClaim } = useAuth()
  const canCreateProjectLifecycle = hasPermissionClaim(
    'Permissions.ProjectLifecycles.Create',
  )
  const showActions = canCreateProjectLifecycle

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading project lifecycles',
      )
      console.error(error)
    }
  }, [error, messageApi])

  const columns = useMemo<ColumnDef<ProjectLifecycleListDto, any>[]>(
    () => [
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        cell: ({ row }) => (
          <Link href={`./project-lifecycles/${row.original.key}`}>
            {row.original.name}
          </Link>
        ),
      },
      {
        id: 'state',
        accessorKey: 'state.name',
        header: 'State',
        size: 100,
        meta: { filterType: 'set' },
      },
      {
        id: 'phaseCount',
        accessorKey: 'phaseCount',
        header: 'Phase Count',
        size: 120,
      },
    ],
    [],
  )

  const refresh = async () => {
    refetch()
  }

  const actions = !showActions ? null : (
    <>
      {canCreateProjectLifecycle && (
        <Button onClick={() => setOpenCreateForm(true)}>
          Create Project Lifecycle
        </Button>
      )}
    </>
  )

  const onCreateFormClosed = (wasCreated: boolean) => {
    setOpenCreateForm(false)
    if (wasCreated) {
      refetch()
    }
  }

  return (
    <>
      <PageTitle title="Project Lifecycles" actions={actions} />

      <WaydGrid2
        columns={columns}
        data={lifecycleData ?? []}
        onRefresh={refresh}
        isLoading={isLoading}
        csvFileName="project-lifecycles"
      />
      {openCreateForm && (
        <CreateProjectLifecycleForm
          onFormComplete={() => onCreateFormClosed(true)}
          onFormCancel={() => onCreateFormClosed(false)}
        />
      )}
    </>
  )
}

const ProjectLifecyclesPageWithAuthorization = authorizePage(
  ProjectLifecyclesPage,
  'Permission',
  'Permissions.ProjectLifecycles.View',
)

export default ProjectLifecyclesPageWithAuthorization
