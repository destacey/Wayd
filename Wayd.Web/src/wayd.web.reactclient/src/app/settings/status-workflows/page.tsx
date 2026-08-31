'use client'

import { PageTitle } from '@/src/components/common'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import WorkflowAssignmentsPanel from './_components/workflow-assignments-panel'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { StatusWorkflowListDto } from '@/src/services/wayd-api'
import { useGetStatusWorkflowsQuery } from '@/src/store/features/common/status-workflows-api'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { Button } from 'antd'
import Link from 'next/link'
import { useEffect, useMemo, useState } from 'react'
import CreateStatusWorkflowForm from './_components/create-status-workflow-form'
import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError } from '@/src/utils'

const StatusWorkflowsPage = () => {
  useDocumentTitle('Settings - Status Workflows')
  const [openCreateForm, setOpenCreateForm] = useState<boolean>(false)

  const messageApi = useMessage()

  const {
    data: statusWorkflowData,
    isLoading,
    error,
    refetch,
  } = useGetStatusWorkflowsQuery(undefined)

  const { hasPermissionClaim } = useAuth()
  const canCreateStatusWorkflow = hasPermissionClaim(
    'Permissions.StatusWorkflows.Create',
  )

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading status workflows',
      )
      console.error(error)
    }
  }, [error, messageApi])

  // Owner, category and alias arrive as navigation objects rather than flat
  // fields, so every one of those columns needs an accessorFn — a dotted
  // accessorKey would be read as a literal key name.
  const columns = useMemo<ColumnDef<StatusWorkflowListDto, any>[]>(
    () => [
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        cell: ({ row }) => (
          <Link href={`./status-workflows/${row.original.key}`}>
            {row.original.name}
          </Link>
        ),
      },
      {
        id: 'owner',
        accessorFn: (row) => row.owner?.name,
        header: 'Owner',
        size: 160,
        meta: { filterType: 'set' },
      },
      {
        id: 'state',
        accessorKey: 'state',
        header: 'State',
        size: 120,
        meta: { filterType: 'set' },
      },
      {
        id: 'statusCount',
        accessorKey: 'statusCount',
        header: 'Statuses',
        size: 110,
      },
      {
        id: 'isSystem',
        accessorKey: 'isSystem',
        header: 'System',
        size: 100,
        meta: { columnType: 'yesNo' },
      },
      {
        id: 'isAssigned',
        accessorKey: 'isAssigned',
        header: 'Assigned',
        size: 110,
        meta: { columnType: 'yesNo' },
      },
    ],
    [],
  )

  const refresh = async () => {
    refetch()
  }

  const actions = !canCreateStatusWorkflow ? null : (
    <Button onClick={() => setOpenCreateForm(true)}>Create Workflow</Button>
  )

  const onCreateFormClosed = (wasCreated: boolean) => {
    setOpenCreateForm(false)
    if (wasCreated) {
      refetch()
    }
  }

  return (
    <div className="page-gutters">
      <PageTitle title="Status Workflows" actions={actions} />

      <div style={{ marginBottom: 16 }}>
        <WorkflowAssignmentsPanel />
      </div>

      <WaydGrid
        columns={columns}
        data={statusWorkflowData ?? []}
        onRefresh={refresh}
        isLoading={isLoading}
        persistStateKey="settings-status-workflows"
        csvFileName="status-workflows"
      />
      {openCreateForm && (
        <CreateStatusWorkflowForm
          onFormComplete={() => onCreateFormClosed(true)}
          onFormCancel={() => onCreateFormClosed(false)}
        />
      )}
    </div>
  )
}

const StatusWorkflowsPageWithAuthorization = authorizePage(
  StatusWorkflowsPage,
  'Permission',
  'Permissions.StatusWorkflows.View',
)

export default StatusWorkflowsPageWithAuthorization
