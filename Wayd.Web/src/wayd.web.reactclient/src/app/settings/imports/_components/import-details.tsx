'use client'

import { FC, useMemo, useState } from 'react'
import { Alert, Flex, Segmented, Typography } from 'antd'
import Link from 'next/link'
import { METRIC_CARD_FLEX, MetricCard } from '@/src/components/common/metrics'
import {
  WaydGrid,
  type ServerPageRequest,
} from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { getImportsClient } from '@/src/services/clients'
import {
  ImportProcessDto,
  ImportProcessRowDto,
  ImportRowStatus,
} from '@/src/services/wayd-api'
import {
  ImportRowStatusTag,
  importRowStatusLabel,
  isRunning,
} from './import-status-tag'
import {
  importRecordHref,
  importRecordLabel,
  importRecordText,
} from './import-record-link'

const POLLING_INTERVAL_MS = 5000

type RowFilter = 'all' | ImportRowStatus

/** The fields `GetImportProcessRowsQuery.Fields` sorts and filters by. */
const ROW_FIELDS = [
  'rowNumber',
  'importId',
  'status',
  'error',
  'warning',
  'attemptedOn',
] as const

const ROW_STATUSES = [
  ImportRowStatus.Failed,
  ImportRowStatus.Succeeded,
  ImportRowStatus.Pending,
  ImportRowStatus.Cancelled,
]

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

  // The run and the status shown make the list (`listKey` below): the default
  // moving to Failed on its own when a running import rejects its first row
  // starts it over at the top of the rejected rows, as a click on Failed does.
  const status = rowFilter === 'all' ? undefined : rowFilter
  const fetchRows = async ({
    page,
    pageSize,
    filter,
    sort,
  }: ServerPageRequest) => {
    const result = await getImportsClient().getRows(
      importProcess.id,
      status,
      page,
      pageSize,
      filter,
      sort,
    )
    return { items: result.items, totalCount: result.totalCount }
  }

  const columns = useMemo<ColumnDef<ImportProcessRowDto, any>[]>(
    () => [
      {
        id: 'rowNumber',
        accessorKey: 'rowNumber',
        header: 'Line',
        size: 80,
        meta: { filterType: 'number' },
      },
      {
        id: 'importId',
        accessorKey: 'importId',
        header: 'Import Id',
        size: 160,
      },
      // A preflight's records were rolled back, so it has nothing to link to.
      ...(isPreflight
        ? []
        : [
            {
              id: 'createdRecord',
              accessorFn: (row) => importRecordText(row),
              header: 'Wayd Record',
              size: 240,
              cell: ({ row }) => {
                const { createdRecord } = row.original
                if (!createdRecord)
                  return (
                    <Typography.Text type="secondary">
                      {importRecordText(row.original)}
                    </Typography.Text>
                  )

                const label = importRecordLabel(createdRecord)
                const href = importRecordHref(createdRecord)
                return href ? <Link href={href}>{label}</Link> : label
              },
            } satisfies ColumnDef<ImportProcessRowDto, any>,
          ]),
      {
        id: 'status',
        accessorKey: 'status',
        header: 'Status',
        size: 130,
        // The server filters by the code; the options label it.
        meta: {
          filterType: 'set',
          filterOptions: ROW_STATUSES.map((value) => ({
            value,
            label: importRowStatusLabel(value, isPreflight),
          })),
        },
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
          ...ROW_STATUSES.map((status) => ({
            label: importRowStatusLabel(status, isPreflight),
            value: status,
          })),
        ]}
      />

      <WaydGrid
        columns={columns}
        server={{
          fetchPage: fetchRows,
          listKey: [importProcess.id, rowFilter],
          fields: ROW_FIELDS,
          noun: 'rows',
          // The rows change while a run goes, and once more when it finishes:
          // the last poll can land just before.
          refreshInterval: running ? POLLING_INTERVAL_MS : undefined,
          refreshKey: importProcess.status,
        }}
        persistStateKey="settings-imports-rows"
        emptyMessage="No rows match this filter."
      />
    </div>
  )
}

export default ImportDetails
