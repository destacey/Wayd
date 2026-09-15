'use client'

import { FC, useMemo, useState } from 'react'
import { Alert, Flex, Segmented, Typography } from 'antd'
import Link from 'next/link'
import { METRIC_CARD_FLEX, MetricCard } from '@/src/components/common/metrics'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import {
  ImportProcessDto,
  ImportProcessRowDto,
  ImportRowStatus,
} from '@/src/services/wayd-api'
import { useGetImportProcessRowsQuery } from '@/src/store/features/admin/imports-api'
import {
  ImportRowStatusTag,
  importRowStatusLabel,
  isRunning,
} from './import-status-tag'

// The server caps a row page at 500. One max-size page, filtered client-side, matching the other
// settings grids.
const ROW_PAGE_SIZE = 500

const POLLING_INTERVAL_MS = 5000

type RowFilter = 'all' | ImportRowStatus

export interface ImportDetailsProps {
  importProcess: ImportProcessDto
}

/** The outcome of one run: its counts, why it stopped if it did, and every row. */
const ImportDetails: FC<ImportDetailsProps> = ({ importProcess }) => {
  const [chosenFilter, setChosenFilter] = useState<RowFilter | null>(null)

  const running = isRunning(importProcess.status)
  const { isPreflight } = importProcess

  // Opens on the rejected rows when there are any: that is the reason someone opens this at all, and
  // paging through thousands of applied rows to find six is the wrong default. Derived rather than set
  // from an effect, so the default follows the run until the reader picks something themselves.
  const rowFilter: RowFilter =
    chosenFilter ??
    (importProcess.failedRowCount > 0 ? ImportRowStatus.Failed : 'all')

  const { data: rows, isLoading: rowsLoading } = useGetImportProcessRowsQuery(
    {
      importProcessId: importProcess.id,
      status: rowFilter === 'all' ? undefined : rowFilter,
      pageSize: ROW_PAGE_SIZE,
      runStatus: importProcess.status,
    },
    {
      pollingInterval: running ? POLLING_INTERVAL_MS : 0,
      skipPollingIfUnfocused: true,
    },
  )

  const columns = useMemo<ColumnDef<ImportProcessRowDto, any>[]>(
    () => [
      {
        id: 'rowNumber',
        accessorKey: 'rowNumber',
        header: 'Line',
        size: 80,
      },
      {
        id: 'importId',
        accessorKey: 'importId',
        header: 'Import Id',
        size: 160,
      },
      {
        id: 'status',
        accessorFn: (row) => importRowStatusLabel(row.status, isPreflight),
        header: 'Status',
        size: 130,
        meta: { filterType: 'set' },
        cell: ({ row }) => (
          <ImportRowStatusTag
            status={row.original.status}
            isPreflight={isPreflight}
          />
        ),
      },
      {
        id: 'error',
        accessorKey: 'error',
        header: 'Reason',
        size: 380,
      },
      {
        id: 'warning',
        accessorKey: 'warning',
        header: 'Warning',
        size: 280,
      },
      {
        id: 'attemptedOn',
        accessorKey: 'attemptedOn',
        header: 'Attempted',
        meta: { columnType: 'dateTime' },
      },
    ],
    [isPreflight],
  )

  const rowOverflow = rows && rows.totalCount > rows.rows.length

  return (
    // A plain block, not a Flex column: WaydGrid takes its height from an inline pixel value that the
    // flex algorithm discards when the container has no resolved height, and the grid then renders
    // headers with no rows.
    <div>
      {running && (
        <Alert
          type="info"
          showIcon
          title={
            isPreflight
              ? 'This check is still running. The page updates as it goes.'
              : 'This import is still running. The page updates as it goes.'
          }
          style={{ marginBottom: 16 }}
        />
      )}

      {importProcess.appliedImportProcessId && (
        <Alert
          type="success"
          showIcon
          title="This file has been imported."
          description="The rows this check covered were submitted for real. The import has its own page with its own outcome."
          action={
            <Link
              href={`/settings/imports/${importProcess.appliedImportProcessId}`}
            >
              View Import
            </Link>
          }
          style={{ marginBottom: 16 }}
        />
      )}

      {isPreflight && !running && (
        <Alert
          type="info"
          showIcon
          title="This was a preflight. Nothing was imported."
          description="Every row went through the same checks an import runs, against the data as it stood at the time. It is advice, not a promise: records can change before the file is imported, so the import checks every row again."
          style={{ marginBottom: 16 }}
        />
      )}

      <Flex gap={12} wrap style={{ marginBottom: 16 }}>
        <MetricCard
          title={isPreflight ? 'Passed' : 'Applied'}
          value={importProcess.succeededRowCount}
          cardStyle={METRIC_CARD_FLEX}
          tooltip={
            isPreflight
              ? 'Rows that passed every check. Importing the file would create a record for each, if nothing changes first.'
              : 'Rows this import created a record for. These are never reapplied by a resume or a retry.'
          }
        />
        <MetricCard
          title="Rejected"
          value={importProcess.failedRowCount}
          cardStyle={METRIC_CARD_FLEX}
          valueStyle={
            importProcess.failedRowCount
              ? { color: 'var(--ant-color-error)' }
              : undefined
          }
          tooltip={
            isPreflight
              ? 'Rows an import would reject. Their reason is listed below.'
              : 'Rows that changed nothing. Their reason is listed below, and their data is kept so they can be retried once the file is fixed.'
          }
        />
        <MetricCard
          title={isPreflight ? 'Not Checked' : 'Not Applied'}
          value={importProcess.unappliedRowCount}
          cardStyle={METRIC_CARD_FLEX}
          tooltip={
            isPreflight
              ? 'Rows the check never reached — it was stopped or it failed partway.'
              : 'Rows the run never reached — it was stopped or it failed partway. A resume picks up exactly these.'
          }
        />
      </Flex>

      {importProcess.error && (
        <Alert
          type="error"
          showIcon
          title={importProcess.error}
          style={{ marginBottom: 16 }}
        />
      )}

      <Segmented<RowFilter>
        style={{ marginBottom: 16 }}
        value={rowFilter}
        onChange={setChosenFilter}
        options={[
          { label: 'All', value: 'all' },
          ...[
            ImportRowStatus.Failed,
            ImportRowStatus.Succeeded,
            ImportRowStatus.Pending,
            ImportRowStatus.Cancelled,
          ].map((status) => ({
            label: importRowStatusLabel(status, isPreflight),
            value: status,
          })),
        ]}
      />

      <WaydGrid
        columns={columns}
        data={rows?.rows}
        isLoading={rowsLoading}
        persistStateKey="settings-imports-rows"
        csvFileName="import-rows"
        initialSorting={[{ id: 'rowNumber', desc: false }]}
        emptyMessage="No rows match this filter."
        leftSlot={
          rowOverflow ? (
            <Typography.Text type="warning">
              {`Showing the first ${rows.rows.length} of ${rows.totalCount} rows.`}
            </Typography.Text>
          ) : undefined
        }
      />
    </div>
  )
}

export default ImportDetails
