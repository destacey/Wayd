'use client'

import { PageTitle } from '@/src/components/common'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { createActionsColumn } from '@/src/components/common/wayd-grid-core'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import {
  RecordTypeDto,
  WorkflowOwnership,
  WorkflowSchemeListDto,
} from '@/src/services/wayd-api'
import {
  useDeleteWorkflowSchemeMutation,
  useGetRecordTypesQuery,
  useGetWorkflowSchemesQuery,
  useSetRecordTypeActiveMutation,
} from '@/src/store/features/common/workflow-schemes-api'
import { toApiError } from '@/src/utils'
import { App, Button, Flex, Tabs } from 'antd'
import type { ItemType } from 'antd/es/menu/interface'
import Link from 'next/link'
import { useState } from 'react'
import RecordTypeForm from './_components/record-type-form'
import WorkflowSchemeForm, {
  type WorkflowSchemeFormMode,
} from './_components/workflow-scheme-form'

/**
 * Schemes and the record types they name, for owner types whose records come
 * in types. Owner types without record types pick a workflow on the Status
 * Workflows page instead.
 */
const WorkflowSchemesPage = () => {
  useDocumentTitle('Settings - Workflow Schemes')
  const messageApi = useMessage()
  const { modal } = App.useApp()

  const [schemeForm, setSchemeForm] = useState<WorkflowSchemeFormMode | null>(
    null,
  )
  const [recordTypeForm, setRecordTypeForm] = useState<{
    recordType?: RecordTypeDto
  } | null>(null)

  const schemes = useGetWorkflowSchemesQuery(undefined)
  const recordTypes = useGetRecordTypesQuery(undefined)
  const [deleteScheme] = useDeleteWorkflowSchemeMutation()
  const [setRecordTypeActive] = useSetRecordTypeActiveMutation()

  const { hasPermissionClaim } = useAuth()
  const canCreate = hasPermissionClaim('Permissions.StatusWorkflows.Create')
  const canUpdate = hasPermissionClaim('Permissions.StatusWorkflows.Update')
  const canDelete = hasPermissionClaim('Permissions.StatusWorkflows.Delete')

  const confirmDelete = (scheme: WorkflowSchemeListDto) =>
    modal.confirm({
      title: 'Delete Workflow Scheme',
      content: `Are you sure you want to delete "${scheme.name}"?`,
      okText: 'Delete',
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await deleteScheme(scheme.id).unwrap()
          messageApi.success('Workflow scheme deleted.')
        } catch (error) {
          messageApi.error(
            toApiError(error).detail ??
              'An error occurred while deleting the workflow scheme.',
          )
        }
      },
    })

  const toggleActive = async (recordType: RecordTypeDto) => {
    try {
      await setRecordTypeActive({
        id: recordType.id,
        isActive: !recordType.isActive,
      }).unwrap()
      messageApi.success(
        recordType.isActive
          ? 'Record type deactivated.'
          : 'Record type activated.',
      )
    } catch (error) {
      messageApi.error(
        toApiError(error).detail ??
          'An error occurred while updating the record type.',
      )
    }
  }

  const schemeColumns: ColumnDef<WorkflowSchemeListDto, any>[] = [
    createActionsColumn<WorkflowSchemeListDto>({
      unavailable: !canCreate && !canUpdate && !canDelete,
      getItems: (scheme) =>
        [
          canUpdate && {
            key: 'edit',
            label: 'Edit',
            onClick: () => setSchemeForm({ kind: 'edit', scheme }),
          },
          canCreate && {
            key: 'clone',
            label: 'Clone',
            onClick: () => setSchemeForm({ kind: 'clone', scheme }),
          },
          // A scheme a scope follows cannot be deleted, so it is not offered.
          canDelete &&
            scheme.assignedScopeCount === 0 && {
              key: 'delete',
              label: 'Delete',
              danger: true,
              onClick: () => confirmDelete(scheme),
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
        <Link href={`./workflow-schemes/${row.original.key}`}>
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
      id: 'defaultWorkflow',
      accessorFn: (row) => row.defaultWorkflow?.name,
      header: 'Default Workflow',
      size: 200,
    },
    {
      id: 'entryCount',
      accessorKey: 'entryCount',
      header: 'Entries',
      size: 100,
    },
    {
      id: 'assignedScopeCount',
      accessorKey: 'assignedScopeCount',
      header: 'Scopes',
      size: 100,
    },
    {
      id: 'ownership',
      accessorKey: 'ownership',
      header: 'Ownership',
      size: 120,
      meta: { filterType: 'set' },
    },
  ]

  const recordTypeColumns: ColumnDef<RecordTypeDto, any>[] = [
    createActionsColumn<RecordTypeDto>({
      unavailable: !canUpdate,
      getItems: (recordType) =>
        [
          // A managed type takes its name from its source.
          canUpdate &&
            recordType.ownership === WorkflowOwnership.Owned && {
              key: 'rename',
              label: 'Rename',
              onClick: () => setRecordTypeForm({ recordType }),
            },
          canUpdate && {
            key: 'active',
            label: recordType.isActive ? 'Deactivate' : 'Activate',
            onClick: () => toggleActive(recordType),
          },
        ].filter(Boolean) as ItemType[],
    }),
    { id: 'key', accessorKey: 'key', header: 'Key', size: 90 },
    { id: 'name', accessorKey: 'name', header: 'Name', size: 220 },
    {
      id: 'owner',
      accessorFn: (row) => row.owner?.name,
      header: 'Owner',
      size: 160,
      meta: { filterType: 'set' },
    },
    {
      id: 'ownership',
      accessorKey: 'ownership',
      header: 'Ownership',
      size: 120,
      meta: { filterType: 'set' },
    },
    {
      id: 'isActive',
      accessorKey: 'isActive',
      header: 'Active',
      size: 100,
      meta: { columnType: 'yesNo' },
    },
  ]

  const actions = canCreate ? (
    <Flex gap={8}>
      <Button onClick={() => setRecordTypeForm({})}>Create Record Type</Button>
      <Button onClick={() => setSchemeForm({ kind: 'create' })}>
        Create Scheme
      </Button>
    </Flex>
  ) : null

  return (
    <div className="page-gutters">
      <PageTitle title="Workflow Schemes" actions={actions} />

      <Tabs
        items={[
          {
            key: 'schemes',
            label: 'Schemes',
            children: (
              <WaydGrid
                columns={schemeColumns}
                data={schemes.data ?? []}
                onRefresh={async () => {
                  schemes.refetch()
                }}
                isLoading={schemes.isLoading}
                persistStateKey="settings-workflow-schemes"
                csvFileName="workflow-schemes"
              />
            ),
          },
          {
            key: 'record-types',
            label: 'Record Types',
            children: (
              <WaydGrid
                columns={recordTypeColumns}
                data={recordTypes.data ?? []}
                onRefresh={async () => {
                  recordTypes.refetch()
                }}
                isLoading={recordTypes.isLoading}
                persistStateKey="settings-record-types"
                csvFileName="record-types"
              />
            ),
          },
        ]}
      />

      {schemeForm && (
        <WorkflowSchemeForm
          mode={schemeForm}
          onFormComplete={() => setSchemeForm(null)}
          onFormCancel={() => setSchemeForm(null)}
        />
      )}
      {recordTypeForm && (
        <RecordTypeForm
          recordType={recordTypeForm.recordType}
          onFormComplete={() => setRecordTypeForm(null)}
          onFormCancel={() => setRecordTypeForm(null)}
        />
      )}
    </div>
  )
}

const WorkflowSchemesPageWithAuthorization = authorizePage(
  WorkflowSchemesPage,
  'Permission',
  'Permissions.StatusWorkflows.View',
)

export default WorkflowSchemesPageWithAuthorization
