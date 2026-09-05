'use client'

import { PageTitle } from '@/src/components/common'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import useAuth from '@/src/components/contexts/auth'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  StatusWorkflowListDto,
  WorkflowAssignmentDto,
} from '@/src/services/wayd-api'
import {
  useGetStatusWorkflowsQuery,
  useGetWorkflowAssignmentsQuery,
} from '@/src/store/features/common/status-workflows-api'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { createActionsColumn } from '@/src/components/common/wayd-grid-core'
import type { ItemType } from 'antd/es/menu/interface'
import { Button } from 'antd'
import Link from 'next/link'
import { useEffect, useMemo, useState } from 'react'
import CloneStatusWorkflowForm from './_components/clone-status-workflow-form'
import ReassignWorkflowModal from './_components/reassign-workflow-modal'
import CreateStatusWorkflowForm from './_components/create-status-workflow-form'
import { useMessage } from '@/src/components/contexts/messaging'
import { isApiError } from '@/src/utils'

const StatusWorkflowsPage = () => {
  useDocumentTitle('Settings - Status Workflows')
  const [openCreateForm, setOpenCreateForm] = useState<boolean>(false)
  const [cloning, setCloning] = useState<StatusWorkflowListDto | null>(null)
  const [reassigning, setReassigning] = useState<WorkflowAssignmentDto | null>(
    null,
  )

  const messageApi = useMessage()

  const {
    data: statusWorkflowData,
    isLoading,
    error,
    refetch,
  } = useGetStatusWorkflowsQuery(undefined)

  // Reassignment is keyed by assignment, not workflow, and the list row carries
  // only a flag — so the assignment is matched back by owner type.
  const { data: assignments, refetch: refetchAssignments } =
    useGetWorkflowAssignmentsQuery(undefined)

  const { hasPermissionClaim } = useAuth()
  const canCreateStatusWorkflow = hasPermissionClaim(
    'Permissions.StatusWorkflows.Create',
  )
  const canUpdateStatusWorkflow = hasPermissionClaim(
    'Permissions.StatusWorkflows.Update',
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
      // Cloning is on the row because it is the only way to change a seeded
      // workflow, and every workflow that ships is seeded.
      createActionsColumn<StatusWorkflowListDto>({
        unavailable: !canCreateStatusWorkflow && !canUpdateStatusWorkflow,
        getItems: (workflow) =>
          [
            canCreateStatusWorkflow && {
              key: 'clone',
              label: 'Clone',
              onClick: () => setCloning(workflow),
            },
            // Only on the workflow a type is actually using: changing an unused
            // workflow's assignment is meaningless.
            canUpdateStatusWorkflow &&
              workflow.isAssigned && {
                key: 'change',
                label: 'Change workflow',
                onClick: () => {
                  const assignment = assignments?.find(
                    (a) => a.workflow.id === workflow.id,
                  )
                  if (assignment) setReassigning(assignment)
                },
              },
          ].filter(Boolean) as ItemType[],
      }),
      { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
      {
        id: 'name',
        accessorKey: 'name',
        header: 'Name',
        size: 250,
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
    [assignments, canCreateStatusWorkflow, canUpdateStatusWorkflow],
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

      <WaydGrid
        columns={columns}
        data={statusWorkflowData ?? []}
        onRefresh={refresh}
        isLoading={isLoading}
        persistStateKey="settings-status-workflows"
        csvFileName="status-workflows"
      />
      {reassigning && (
        <ReassignWorkflowModal
          assignment={reassigning}
          onFormComplete={() => {
            setReassigning(null)
            refetchAssignments()
            refetch()
          }}
          onFormCancel={() => setReassigning(null)}
        />
      )}

      {cloning && (
        <CloneStatusWorkflowForm
          statusWorkflow={cloning}
          onFormComplete={() => {
            setCloning(null)
            refetch()
          }}
          onFormCancel={() => setCloning(null)}
        />
      )}

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

