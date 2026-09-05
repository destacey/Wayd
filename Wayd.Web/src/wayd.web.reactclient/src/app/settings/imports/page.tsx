'use client'

import { useMemo, useState } from 'react'
import { Button, Flex, Typography } from 'antd'
import type { ItemType } from 'antd/es/menu/interface'
import PageTitle from '@/src/components/common/page-title'
import { METRIC_CARD_FLEX, MetricCard } from '@/src/components/common/metrics'
import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { useDocumentTitle } from '@/src/hooks'
import { ImportProcessDto, ImportProcessStatus } from '@/src/services/wayd-api'
import { useGetImportProcessesQuery } from '@/src/store/features/admin/imports-api'
import ImportDetailsDrawer from './_components/import-details-drawer'
import {
  ImportStatusTag,
  importStatusLabel,
  isRunning,
} from './_components/import-status-tag'
import useImportActions from './_components/use-import-actions'

// The server caps a listing at 500. Fetch one max-size page and let the grid filter and sort
// client-side; the overflow note carries the true total if history ever runs deeper than that.
const IMPORT_PAGE_SIZE = 500

// A run reports progress at every chunk boundary, so a page watching one wants to see it move.
const POLLING_INTERVAL_MS = 5000

const ImportsPage = () => {
  useDocumentTitle('Imports')
  const [viewingImportId, setViewingImportId] = useState<string | null>(null)
  const [drawerOpen, setDrawerOpen] = useState(false)

  const { data, isLoading, refetch } = useGetImportProcessesQuery(
    { pageSize: IMPORT_PAGE_SIZE },
    { pollingInterval: POLLING_INTERVAL_MS },
  )

  const { handleCancel, handleResume, handleRetryFailed } = useImportActions()

  const imports = data?.processes
  const activeCount = imports?.filter((i) => isRunning(i.status)).length ?? 0
  const failedCount =
    imports?.filter(
      (i) =>
        i.status === ImportProcessStatus.Failed ||
        i.status === ImportProcessStatus.PartiallySucceeded,
    ).length ?? 0
  const rejectedRowCount =
    imports?.reduce((total, i) => total + i.failedRowCount, 0) ?? 0

  const closeDetailsDrawer = () => {
    setDrawerOpen(false)
    setViewingImportId(null)
  }

  const columns = useMemo<ColumnDef<ImportProcessDto, any>[]>(() => {
    const openDetailsDrawer = (id: string) => {
      setViewingImportId(id)
      setDrawerOpen(true)
    }

    return [
      createActionsColumn<ImportProcessDto>({
        ariaLabel: 'Import actions',
        getItems: (importProcess) => {
          const items: ItemType[] = []

          // Seeing a run and acting on it are separate grants: someone overseeing every import type
          // reads this row but may not change the records it created. The server refuses either way;
          // this keeps the menu from offering what it would refuse.
          if (!importProcess.canManage) return items

          if (isRunning(importProcess.status)) {
            items.push({
              key: 'cancel',
              label: 'Stop',
              danger: true,
              onClick: () => handleCancel(importProcess),
            })

            return items
          }

          if (importProcess.unappliedRowCount > 0) {
            items.push({
              key: 'resume',
              label: 'Resume',
              onClick: () => handleResume(importProcess),
            })
          }

          if (importProcess.failedRowCount > 0) {
            items.push({
              key: 'retry-failed',
              label: 'Retry Rejected Rows',
              onClick: () => handleRetryFailed(importProcess),
            })
          }

          return items
        },
      }),
      {
        id: 'displayName',
        accessorKey: 'displayName',
        header: 'Import',
        size: 200,
        meta: { filterType: 'set' },
        cell: ({ row }) => (
          <Button
            type="link"
            style={{ padding: 0, height: 'auto', fontSize: 'inherit' }}
            onClick={() => openDetailsDrawer(row.original.id)}
          >
            {row.original.displayName}
          </Button>
        ),
      },
      {
        id: 'status',
        accessorFn: (row) => importStatusLabel(row.status),
        header: 'Status',
        size: 170,
        meta: { filterType: 'set' },
        cell: ({ row }) => <ImportStatusTag status={row.original.status} />,
      },
      {
        id: 'submittedByName',
        accessorFn: (row) => row.submittedByName ?? row.submittedByUserId,
        header: 'Submitted By',
        size: 180,
        meta: { filterType: 'set' },
      },
      {
        id: 'submittedOn',
        accessorKey: 'submittedOn',
        header: 'Submitted',
        meta: { columnType: 'dateTime' },
      },
      {
        id: 'totalRowCount',
        accessorKey: 'totalRowCount',
        header: 'Rows',
        size: 90,
      },
      {
        id: 'succeededRowCount',
        accessorKey: 'succeededRowCount',
        header: 'Applied',
        size: 100,
      },
      {
        id: 'failedRowCount',
        accessorKey: 'failedRowCount',
        header: 'Rejected',
        size: 110,
        cell: ({ row }) =>
          row.original.failedRowCount > 0 ? (
            <span style={{ color: 'var(--ant-color-error)' }}>
              {row.original.failedRowCount}
            </span>
          ) : (
            row.original.failedRowCount
          ),
      },
      {
        id: 'unappliedRowCount',
        accessorKey: 'unappliedRowCount',
        header: 'Not Applied',
        size: 120,
      },
      {
        id: 'completedOn',
        accessorKey: 'completedOn',
        header: 'Finished',
        meta: { columnType: 'dateTime' },
      },
    ]
  }, [handleCancel, handleResume, handleRetryFailed])

  const overflow = data && data.totalCount > (imports?.length ?? 0)

  return (
    <div className="page-gutters">
      <PageTitle title="Imports" />
      <Flex gap={12} wrap style={{ marginBottom: 16 }}>
        <MetricCard
          title="In Progress"
          value={activeCount}
          loading={isLoading}
          cardStyle={METRIC_CARD_FLEX}
          tooltip="Imports queued or being applied right now. A large file is applied by a background worker in chunks, so this can sit above zero for a while."
        />
        <MetricCard
          title="Needs Attention"
          value={failedCount}
          loading={isLoading}
          cardStyle={METRIC_CARD_FLEX}
          valueStyle={
            failedCount ? { color: 'var(--ant-color-error)' } : undefined
          }
          tooltip="Imports that failed outright or applied only part of their file. Open one to see which rows were rejected and why, then retry those rows once the file is fixed."
        />
        <MetricCard
          title="Rejected Rows"
          value={rejectedRowCount}
          loading={isLoading}
          cardStyle={METRIC_CARD_FLEX}
          tooltip="Rows rejected across every import shown. A rejected row changed nothing — its data is kept so it can be retried, until the import passes its 30-day retention window."
        />
      </Flex>
      <WaydGrid
        columns={columns}
        data={imports}
        onRefresh={refetch}
        isLoading={isLoading}
        persistStateKey="settings-imports"
        csvFileName="imports"
        initialSorting={[{ id: 'submittedOn', desc: true }]}
        emptyMessage="No imports have been submitted."
        leftSlot={
          overflow ? (
            <Typography.Text type="warning">
              {`Showing the first ${imports?.length} of ${data.totalCount} imports.`}
            </Typography.Text>
          ) : undefined
        }
      />
      {viewingImportId !== null && (
        <ImportDetailsDrawer
          importProcessId={viewingImportId}
          drawerOpen={drawerOpen}
          onDrawerClose={closeDetailsDrawer}
        />
      )}
    </div>
  )
}

// No authorizePage claim: there is no single import permission to name, and the listing already returns
// only the import types this viewer may submit — an empty page for someone with none.
export default ImportsPage
