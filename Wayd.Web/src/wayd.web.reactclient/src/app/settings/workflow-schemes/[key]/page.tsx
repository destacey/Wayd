'use client'

import { PageActions } from '@/src/components/common'
import { LabeledContent } from '@/src/components/common/content'
import { RecordLayout, RecordSection } from '@/src/components/common/record'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { createActionsColumn } from '@/src/components/common/wayd-grid-core'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { WorkflowSchemeEntryDto } from '@/src/services/wayd-api'
import {
  useDeleteWorkflowSchemeMutation,
  useGetRecordTypesQuery,
  useGetWorkflowSchemeQuery,
  useSetWorkflowSchemeEntryMutation,
} from '@/src/store/features/common/workflow-schemes-api'
import { isApiError } from '@/src/utils'
import { App, Button, Flex } from 'antd'
import type { ItemType } from 'antd/es/menu/interface'
import Link from 'next/link'
import { notFound, useRouter } from 'next/navigation'
import { use, useEffect, useState } from 'react'
import ChangeSchemeWorkflowModal, {
  type SchemeWorkflowChange,
} from '../_components/change-scheme-workflow-modal'
import WorkflowSchemeForm, {
  type WorkflowSchemeFormMode,
} from '../_components/workflow-scheme-form'
import WorkflowSchemeDetailsLoading from './loading'

enum WorkflowSchemeSections {
  Entries = 'entries',
}

const WorkflowSchemeDetailsPage = (props: {
  params: Promise<{ key: string }>
}) => {
  const { key } = use(props.params)
  const router = useRouter()
  const messageApi = useMessage()
  const { modal } = App.useApp()

  const [schemeForm, setSchemeForm] = useState<WorkflowSchemeFormMode | null>(
    null,
  )
  const [change, setChange] = useState<SchemeWorkflowChange | null>(null)

  const { data: scheme, isLoading, error } = useGetWorkflowSchemeQuery(key)
  const { data: recordTypes } = useGetRecordTypesQuery(scheme?.owner.key, {
    skip: !scheme,
  })
  const [deleteScheme] = useDeleteWorkflowSchemeMutation()
  const [setEntry] = useSetWorkflowSchemeEntryMutation()

  const { hasPermissionClaim } = useAuth()
  const canCreate = hasPermissionClaim('Permissions.StatusWorkflows.Create')
  const canUpdate = hasPermissionClaim('Permissions.StatusWorkflows.Update')
  const canDelete = hasPermissionClaim('Permissions.StatusWorkflows.Delete')

  useDocumentTitle(
    scheme
      ? `${scheme.name} - Workflow Scheme Details`
      : 'Workflow Scheme Details',
  )

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading the workflow scheme',
      )
    }
  }, [error, messageApi])

  if (isLoading) return <WorkflowSchemeDetailsLoading />
  if (!scheme) return notFound()

  const confirmRemoveEntry = (entry: WorkflowSchemeEntryDto) =>
    modal.confirm({
      title: 'Remove Entry',
      content: `${entry.recordType.name} records will follow the default workflow, ${scheme.defaultWorkflow.name}.`,
      okText: 'Remove',
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await setEntry({
            schemeId: scheme.id,
            recordTypeId: entry.recordType.id,
            request: { workflowId: undefined, decisions: [] },
          }).unwrap()
          messageApi.success('Entry removed.')
        } catch (error) {
          messageApi.error(
            (isApiError(error) ? error.detail : undefined) ??
              'An error occurred while removing the entry.',
          )
        }
      },
    })

  const confirmDelete = () =>
    modal.confirm({
      title: 'Delete Workflow Scheme',
      content: `Are you sure you want to delete "${scheme.name}"?`,
      okText: 'Delete',
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          await deleteScheme(scheme.id).unwrap()
          messageApi.success('Workflow scheme deleted.')
          router.push('/settings/workflow-schemes')
        } catch (error) {
          messageApi.error(
            (isApiError(error) ? error.detail : undefined) ??
              'An error occurred while deleting the workflow scheme.',
          )
        }
      },
    })

  const actionItems = [
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
    canUpdate && {
      key: 'default',
      label: 'Change Default Workflow',
      onClick: () => setChange({ kind: 'default' }),
    },
    canDelete &&
      scheme.assignedScopeIds.length === 0 && {
        key: 'delete',
        label: 'Delete',
        danger: true,
        onClick: confirmDelete,
      },
  ].filter(Boolean) as ItemType[]

  const columns: ColumnDef<WorkflowSchemeEntryDto, any>[] = [
    createActionsColumn<WorkflowSchemeEntryDto>({
      unavailable: !canUpdate,
      getItems: (entry) => [
        {
          key: 'change',
          label: 'Change Workflow',
          onClick: () =>
            setChange({ kind: 'entry', recordTypeId: entry.recordType.id }),
        },
        {
          key: 'remove',
          label: 'Remove',
          danger: true,
          onClick: () => confirmRemoveEntry(entry),
        },
      ],
    }),
    {
      id: 'recordType',
      accessorFn: (row) => row.recordType.name,
      header: 'Record Type',
      size: 220,
    },
    {
      id: 'workflow',
      accessorFn: (row) => row.workflow.name,
      header: 'Workflow',
      size: 260,
      cell: ({ row }) => (
        <Link href={`/settings/status-workflows/${row.original.workflow.key}`}>
          {row.original.workflow.name}
        </Link>
      ),
    },
  ]

  const sections: RecordSection[] = [
    {
      id: WorkflowSchemeSections.Entries,
      label: 'Entries',
      count: scheme.entries.length,
    },
  ]

  const facts = (
    <Flex vertical gap={10}>
      <LabeledContent label="Key">{scheme.key}</LabeledContent>
      <LabeledContent label="Owner">{scheme.owner.name}</LabeledContent>
      <LabeledContent
        label="Default Workflow"
        tooltip="The workflow records follow when their record type has no entry."
      >
        <Link href={`/settings/status-workflows/${scheme.defaultWorkflow.key}`}>
          {scheme.defaultWorkflow.name}
        </Link>
      </LabeledContent>
      <LabeledContent
        label="Ownership"
        tooltip="Owned schemes are kept in Wayd. Managed schemes are kept by a sync from their source."
      >
        {scheme.ownership}
      </LabeledContent>
      <LabeledContent
        label="Scopes"
        tooltip="How many scopes follow this scheme. A change to it moves records in each of them."
      >
        {scheme.assignedScopeIds.length}
      </LabeledContent>
      {scheme.description && (
        <LabeledContent label="Description">
          {scheme.description}
        </LabeledContent>
      )}
    </Flex>
  )

  return (
    <>
      <RecordLayout
        sections={sections}
        defaultSection={WorkflowSchemeSections.Entries}
        record={{
          name: scheme.name,
          recordKey: String(scheme.key),
          parent: {
            label: 'Workflow Schemes',
            href: '/settings/workflow-schemes',
          },
          subtitle: 'Workflow Scheme Details',
          actions:
            actionItems.length > 0 ? (
              <PageActions actionItems={actionItems} />
            ) : undefined,
        }}
        facts={facts}
        sectionActions={
          canUpdate ? (
            <Button size="small" onClick={() => setChange({ kind: 'entry' })}>
              Add Entry
            </Button>
          ) : undefined
        }
      >
        {() => (
          <WaydGrid
            columns={columns}
            data={[...scheme.entries]}
            persistStateKey="settings-workflow-scheme-entries"
            csvFileName={`workflow-scheme-${scheme.key}-entries`}
          />
        )}
      </RecordLayout>

      {schemeForm && (
        <WorkflowSchemeForm
          mode={schemeForm}
          onFormComplete={() => setSchemeForm(null)}
          onFormCancel={() => setSchemeForm(null)}
        />
      )}
      {change && (
        <ChangeSchemeWorkflowModal
          scheme={scheme}
          change={change}
          recordTypes={recordTypes ?? []}
          onFormComplete={() => setChange(null)}
          onFormCancel={() => setChange(null)}
        />
      )}
    </>
  )
}

const WorkflowSchemeDetailsPageWithAuthorization = authorizePage(
  WorkflowSchemeDetailsPage,
  'Permission',
  'Permissions.StatusWorkflows.View',
)

export default WorkflowSchemeDetailsPageWithAuthorization
