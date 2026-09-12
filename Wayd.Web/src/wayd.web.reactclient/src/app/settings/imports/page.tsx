'use client'

import { useMemo, useState } from 'react'
import { Button, Flex, Tooltip, Typography } from 'antd'
import { CaretDownOutlined, CaretRightOutlined } from '@ant-design/icons'
import type { ItemType } from 'antd/es/menu/interface'
import Link from 'next/link'
import PageTitle from '@/src/components/common/page-title'
import { METRIC_CARD_FLEX, MetricCard } from '@/src/components/common/metrics'
import {
  WaydGrid,
  createActionsColumn,
} from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import treeGridStyles from '@/src/components/common/wayd-grid/wayd-grid.module.css'
import { useDocumentTitle } from '@/src/hooks'
import { ImportProcessStatus } from '@/src/services/wayd-api'
import { useGetImportProcessesQuery } from '@/src/store/features/admin/imports-api'
import { buildImportRows, ImportListRow } from './_components/import-groups'
import {
  ImportStatusTag,
  importStatusLabel,
  isRunning,
} from './_components/import-status-tag'
import useImportActions from './_components/use-import-actions'

// The server caps a listing at 500. Fetch one max-size page and let the grid filter and sort
// client-side; the overflow note carries the true total if history ever runs deeper than that.
const IMPORT_PAGE_SIZE = 500

// A run reports progress at every chunk boundary, so a page watching one wants to see it move. Only
// while one is going, though: with nothing running there is nothing to animate, and the toolbar's
// refresh covers the case of someone else submitting while this page sits open.
const POLLING_INTERVAL_MS = 5000

const ImportsPage = () => {
  useDocumentTitle('Imports')

  // Whether to keep polling is decided by the query's own result, which cannot be expressed in the
  // same call. Held as state and adjusted during render — React's pattern for deriving state from a
  // changed value, and why this is not an effect: setting state from one renders a second time, which
  // is what the lint rule against it is for.
  const [isPolling, setIsPolling] = useState(false)

  const { data, isLoading, refetch } = useGetImportProcessesQuery(
    { pageSize: IMPORT_PAGE_SIZE },
    {
      pollingInterval: isPolling ? POLLING_INTERVAL_MS : 0,
      // Nothing worth watching while the tab is in the background.
      skipPollingIfUnfocused: true,
    },
  )

  const { handleCancel, handleResume, handleRetryFailed } = useImportActions()

  const imports = data?.processes
  const activeCount = imports?.filter((i) => isRunning(i.status)).length ?? 0

  // The tick that sees the last run finish is also the one that turns polling off.
  if (isPolling !== activeCount > 0) {
    setIsPolling(activeCount > 0)
  }

  const failedCount =
    imports?.filter(
      (i) =>
        i.status === ImportProcessStatus.Failed ||
        i.status === ImportProcessStatus.PartiallySucceeded,
    ).length ?? 0
  const rejectedRowCount =
    imports?.reduce((total, i) => total + i.failedRowCount, 0) ?? 0

  const columns = useMemo<ColumnDef<ImportListRow, any>[]>(() => {
    return [
      createActionsColumn<ImportListRow>({
        ariaLabel: 'Import actions',
        getItems: (importProcess) => {
          const items: ItemType[] = []

          // Seeing a run and acting on it are separate grants: someone overseeing every import type
          // reads this row but may not change the records it created. The server refuses either way;
          // this keeps the menu from offering what it would refuse. A batch rollup never manages:
          // stop, resume and retry each act on one run, and those sit beneath it.
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
        size: 240,
        meta: { filterType: 'set' },
        // The grid renders tree rows flat and leaves depth to the cell, so this column draws the
        // indent and the expander itself. A rollup has no page of its own: it is the runs beneath it.
        cell: ({ row }) => (
          <Flex align="center" gap={0} className={treeGridStyles.nameCell}>
            {Array.from({ length: row.depth }).map((_, index) => (
              <span key={index} className={treeGridStyles.indentSpacer} />
            ))}
            {row.getCanExpand() ? (
              <Button
                type="text"
                size="small"
                icon={
                  row.getIsExpanded() ? (
                    <CaretDownOutlined />
                  ) : (
                    <CaretRightOutlined />
                  )
                }
                onClick={row.getToggleExpandedHandler()}
                className={treeGridStyles.expanderBtn}
                aria-label={row.getIsExpanded() ? 'Collapse batch' : 'Expand batch'}
              />
            ) : (
              <span className={treeGridStyles.indentSpacer} />
            )}
            {row.original.isGroup ? (
              <Typography.Text strong>{row.original.displayName}</Typography.Text>
            ) : (
              <Link href={`/settings/imports/${row.original.id}`}>
                {row.original.displayName}
              </Link>
            )}
          </Flex>
        ),
      },
      {
        id: 'submissionGroupId',
        accessorKey: 'submissionGroupId',
        header: 'Batch',
        size: 110,
        meta: { filterType: 'set' },
        // The id is the caller's; the short form is enough to tell batches apart, and the full
        // value is what a script filtering the API would paste.
        cell: ({ row }) =>
          row.original.submissionGroupId ? (
            <Tooltip title={row.original.submissionGroupId}>
              <Typography.Text code>
                {row.original.submissionGroupId.slice(0, 8)}
              </Typography.Text>
            </Tooltip>
          ) : null,
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

  // Runs posted together fold under one rollup row, collapsed: the batch's outcome is what the
  // reader came for, and its files are a click away.
  const rows = useMemo(() => buildImportRows(imports), [imports])

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
      <WaydGrid<ImportListRow>
        columns={columns}
        data={rows}
        getSubRows={(row) => row.children}
        initialExpanded={false}
        onRefresh={refetch}
        isLoading={isLoading}
        persistStateKey="settings-imports"
        csvFileName="imports"
        initialSorting={[{ id: 'submittedOn', desc: true }]}
        emptyMessage="No imports have been submitted."
        leftSlot={
          overflow ? (
            <Typography.Text type="warning">
              {`Showing the first ${imports?.length} of ${data.totalCount} imports. A batch that straddles the cut shows only the files here.`}
            </Typography.Text>
          ) : undefined
        }
      />
    </div>
  )
}

// No authorizePage claim: there is no single import permission to name, and the listing already returns
// only the import types this viewer may submit — an empty page for someone with none.
export default ImportsPage
