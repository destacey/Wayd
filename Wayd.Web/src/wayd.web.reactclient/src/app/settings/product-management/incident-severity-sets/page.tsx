'use client'

import { PageTitle } from '@/src/components/common'
import {
  ConfigListPanel,
  useSelectedRecord,
} from '@/src/components/common/config-list'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import useAuth from '@/src/components/contexts/auth'
import { useMessage } from '@/src/components/contexts/messaging'
import { authorizePage, requireFeatureFlag } from '@/src/components/hoc'
import { useDocumentTitle } from '@/src/hooks'
import { IncidentSeveritySetDto } from '@/src/services/wayd-api'
import {
  useGetIncidentSeveritySetsQuery,
  useSelectIncidentSeveritySetMutation,
} from '@/src/store/features/product-management/incident-severity-sets-api'
import { isApiError } from '@/src/utils'
import { Button, Space, Tag } from 'antd'
import { useEffect, useState } from 'react'
import {
  CreateIncidentSeveritySetForm,
  IncidentSeveritySetPanel,
} from './_components/incident-severity-set-panel'

const IncidentSeveritySetsPage = () => {
  useDocumentTitle('Product Management - Incident Severity Sets')
  const messageApi = useMessage()
  const [openCreate, setOpenCreate] = useState(false)
  const { selectedId, select, clear } = useSelectedRecord()

  const {
    data: sets,
    isLoading,
    error,
    refetch,
  } = useGetIncidentSeveritySetsQuery(true)

  const selectedSet = sets?.find((s) => s.id === selectedId)
  const { hasPermissionClaim } = useAuth()
  const canCreate = hasPermissionClaim('Permissions.IncidentSeveritySets.Create')
  const canUpdate = hasPermissionClaim('Permissions.IncidentSeveritySets.Update')

  const [selectSet, { isLoading: selecting }] =
    useSelectIncidentSeveritySetMutation()

  useEffect(() => {
    if (error) {
      messageApi.error(
        (isApiError(error) ? error.detail : undefined) ??
          'An error occurred while loading severity sets',
      )
    }
  }, [error, messageApi])

  const columns: ColumnDef<IncidentSeveritySetDto, unknown>[] = [
    {
      id: 'name',
      accessorKey: 'name',
      header: 'Name',
      size: 340,
      cell: ({ row }) => (
        <Space wrap>
          <span>{row.original.name}</span>
          {row.original.isSelected ? <Tag color="blue">Selected</Tag> : null}
          {row.original.isSystem ? <Tag>System</Tag> : null}
          {!row.original.isActive ? <Tag>Inactive</Tag> : null}
        </Space>
      ),
    },
    {
      id: 'levels',
      header: 'Levels',
      size: 420,
      cell: ({ row }) =>
        row.original.levels.map((l) => l.name).join(' → '),
    },
    {
      id: 'select',
      header: '',
      size: 100,
      enableColumnFilter: false,
      cell: ({ row }) =>
        canUpdate && !row.original.isSelected ? (
          <Button
            size="small"
            loading={selecting}
            onClick={(e) => {
              e.stopPropagation()
              void selectSet({ severitySetId: row.original.id })
                .unwrap()
                .then(() => {
                  messageApi.success('Severity set selected.')
                  refetch()
                })
                .catch((err) => {
                  messageApi.error(
                    (isApiError(err) ? err.detail : undefined) ??
                      'Could not select severity set',
                  )
                })
            }}
          >
            Select
          </Button>
        ) : null,
    },
  ]

  const actions = canCreate ? (
    <Button type="primary" onClick={() => setOpenCreate(true)}>
      New set
    </Button>
  ) : null

  return (
    <div className="page-gutters">
      <PageTitle
        title="Incident Severity Sets"
        subtitle="Choose the scale new incidents use. Open incidents keep the level they were given."
        actions={actions}
      />

      <ConfigListPanel
        open={!!selectedSet || (!!selectedId && isLoading)}
        onClose={clear}
        title={selectedSet?.name}
        details={
          selectedSet ? (
            <IncidentSeveritySetPanel
              key={selectedSet.id}
              set={selectedSet}
              onChanged={() => refetch()}
              onDeleted={() => {
                clear()
                refetch()
              }}
            />
          ) : null
        }
        isLoading={isLoading}
      >
        <WaydGrid
          data={sets ?? []}
          columns={columns}
          isLoading={isLoading}
          onRefresh={() => refetch()}
          persistStateKey="settings-incident-severity-sets"
          csvFileName="incident-severity-sets"
          onRowActivate={(row) => select(row.id)}
          activatedRowId={selectedId}
          getRowActivateLabel={(row) => row.name}
        />
      </ConfigListPanel>

      <CreateIncidentSeveritySetForm
        open={openCreate}
        onClose={() => setOpenCreate(false)}
        onCreated={() => {
          setOpenCreate(false)
          refetch()
        }}
      />
    </div>
  )
}

const IncidentSeveritySetsPageWithAuthorization = requireFeatureFlag(
  authorizePage(
    IncidentSeveritySetsPage,
    'Permission',
    'Permissions.IncidentSeveritySets.View',
  ),
  'product-management',
)

export default requireFeatureFlag(
  IncidentSeveritySetsPageWithAuthorization,
  'incident-management',
)
