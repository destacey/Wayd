'use client'

import { FC, useEffect, useMemo, useState } from 'react'
import { Alert, Drawer, Flex, Segmented, Spin, Typography } from 'antd'
import { LabeledContent } from '@/src/components/common/content'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import { useMessage } from '@/src/components/contexts/messaging'
import { getDrawerWidthPixels } from '@/src/utils'
import {
  ImportProcessRowDto,
  ImportRowStatus,
} from '@/src/services/wayd-api'
import {
  useGetImportProcessByIdQuery,
  useGetImportProcessRowsQuery,
} from '@/src/store/features/admin/imports-api'
import {
  ImportRowStatusTag,
  ImportStatusTag,
  importRowStatusLabel,
  isRunning,
} from './import-status-tag'

// The server caps a row page at 500. One max-size page, filtered client-side, matching the other
// settings grids.
const ROW_PAGE_SIZE = 500

const POLLING_INTERVAL_MS = 5000

type RowFilter = 'all' | ImportRowStatus

export interface ImportDetailsDrawerProps {
  importProcessId: string
  drawerOpen: boolean
  onDrawerClose: () => void
}

const ImportDetailsDrawer: FC<ImportDetailsDrawerProps> = ({
  importProcessId,
  drawerOpen,
  onDrawerClose,
}) => {
  const [size] = useState(() => getDrawerWidthPixels())
  const [chosenFilter, setChosenFilter] = useState<RowFilter | null>(null)
  const messageApi = useMessage()

  const {
    data: importProcess,
    isLoading,
    error,
  } = useGetImportProcessByIdQuery(importProcessId)

  const running = importProcess ? isRunning(importProcess.status) : false

  // Opens on the rejected rows when there are any: that is the reason someone opens this at all, and
  // paging through thousands of applied rows to find six is the wrong default. Derived rather than set
  // from an effect, so the default follows the run until the reader picks something themselves.
  const rowFilter: RowFilter =
    chosenFilter ??
    (importProcess && importProcess.failedRowCount > 0
      ? ImportRowStatus.Failed
      : 'all')

  const { data: rows, isLoading: rowsLoading } = useGetImportProcessRowsQuery(
    {
      importProcessId,
      status: rowFilter === 'all' ? undefined : rowFilter,
      pageSize: ROW_PAGE_SIZE,
    },
    {
      // Waits for the run, so the first fetch already carries the right filter.
      skip: !importProcess,
      pollingInterval: running ? POLLING_INTERVAL_MS : 0,
    },
  )

  useEffect(() => {
    if (error) {
      messageApi.error('Failed to load the import.')
    }
  }, [error, messageApi])

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
        accessorFn: (row) => importRowStatusLabel(row.status),
        header: 'Status',
        size: 130,
        meta: { filterType: 'set' },
        cell: ({ row }) => <ImportRowStatusTag status={row.original.status} />,
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
    [],
  )

  const rowOverflow = rows && rows.totalCount > rows.rows.length

  return (
    <Drawer
      title="Import Details"
      width={size}
      open={drawerOpen}
      onClose={onDrawerClose}
      destroyOnHidden
    >
      {isLoading || !importProcess ? (
        <Spin />
      ) : (
        // A plain block, not a Flex column: WaydGrid takes its height from an inline pixel value that
        // the flex algorithm discards when the container has no resolved height, and the grid then
        // renders headers with no rows.
        <div>
          <Flex gap={24} wrap style={{ marginBottom: 16 }}>
            <LabeledContent label="Import">
              {importProcess.displayName}
            </LabeledContent>
            <LabeledContent label="Status">
              <ImportStatusTag status={importProcess.status} />
            </LabeledContent>
            <LabeledContent label="Submitted By">
              {importProcess.submittedByName ?? importProcess.submittedByUserId}
            </LabeledContent>
            <LabeledContent label="Rows">
              {`${importProcess.succeededRowCount} applied · ${importProcess.failedRowCount} rejected · ${importProcess.unappliedRowCount} not applied`}
            </LabeledContent>
          </Flex>

          {importProcess.error && (
            <Alert
              type="error"
              showIcon
              message={importProcess.error}
              style={{ marginBottom: 16 }}
            />
          )}

          <Segmented<RowFilter>
            style={{ marginBottom: 16 }}
            value={rowFilter}
            onChange={setChosenFilter}
            options={[
              { label: 'All', value: 'all' },
              { label: 'Rejected', value: ImportRowStatus.Failed },
              { label: 'Applied', value: ImportRowStatus.Succeeded },
              { label: 'Not Applied', value: ImportRowStatus.Pending },
              { label: 'Cancelled', value: ImportRowStatus.Cancelled },
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
      )}
    </Drawer>
  )
}

export default ImportDetailsDrawer
