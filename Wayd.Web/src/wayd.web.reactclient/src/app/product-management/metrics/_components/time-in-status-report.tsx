'use client'

import {
  WorkflowStatusTag,
  formatTimeInStatus,
} from '@/src/components/common/status-workflows'
import useAuth from '@/src/components/contexts/auth'
import { TimeInStatusReportRowDto } from '@/src/services/wayd-api'
import {
  TimeInStatusRecordType,
  useGetTimeInStatusQuery,
} from '@/src/store/features/product-management/time-in-status-api'
import { Card, Flex, Select, Table, Tooltip, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { FC, useState } from 'react'

const { Text } = Typography

export interface TimeInStatusReportProps {
  /** Start of the window, as ISO-8601. */
  from: string
  /** End of the window, as ISO-8601. */
  to: string
}

/** Each record type with the claim its endpoint requires, so a type the reader cannot see is not offered. */
const recordTypes: {
  value: TimeInStatusRecordType
  label: string
  permission: string
}[] = [
  {
    value: 'versions',
    label: 'Versions',
    permission: 'Permissions.Delivery.View',
  },
  {
    value: 'releases',
    label: 'Releases',
    permission: 'Permissions.Releases.View',
  },
  {
    value: 'releasePackages',
    label: 'Release packages',
    permission: 'Permissions.Delivery.View',
  },
  {
    value: 'deployments',
    label: 'Deployments',
    permission: 'Permissions.Delivery.View',
  },
  {
    value: 'products',
    label: 'Products',
    permission: 'Permissions.Products.View',
  },
]

const durationCell = (seconds: number | undefined) =>
  seconds === undefined || seconds === null ? (
    <Text type="secondary">—</Text>
  ) : (
    formatTimeInStatus(seconds)
  )

const columns: ColumnsType<TimeInStatusReportRowDto> = [
  {
    title: 'Status',
    key: 'status',
    render: (_, row) => (
      <Flex gap={4} align="center">
        <WorkflowStatusTag name={row.name} category={row.category} />
        {!row.inCurrentWorkflow && (
          <Tooltip title="Not in the current workflow: from before a reassignment, or since removed.">
            <Text type="secondary">(earlier)</Text>
          </Tooltip>
        )}
      </Flex>
    ),
  },
  { title: 'Stays', dataIndex: 'visits', key: 'visits', align: 'right' },
  {
    title: 'Average',
    key: 'average',
    align: 'right',
    render: (_, row) => durationCell(row.averageSeconds),
  },
  {
    title: 'Median',
    key: 'median',
    align: 'right',
    render: (_, row) => durationCell(row.medianSeconds),
  },
]

/**
 * How long records of one type stay in each status, over the page's window.
 *
 * Counts only stays that ended in the window — a stay still open has no final length — so a terminal
 * status a record never leaves shows no figures.
 */
const TimeInStatusReport: FC<TimeInStatusReportProps> = ({ from, to }) => {
  const { hasPermissionClaim } = useAuth()
  const options = recordTypes.filter((t) => hasPermissionClaim(t.permission))
  const [selected, setSelected] = useState<TimeInStatusRecordType | undefined>()
  const recordType = selected ?? options[0]?.value

  const { data, isFetching, error } = useGetTimeInStatusQuery(
    { recordType: recordType!, from, to },
    { skip: !recordType },
  )

  if (options.length === 0) return null

  return (
    <Card
      title="Time in status"
      size="small"
      extra={
        <Select
          aria-label="Record type"
          style={{ width: 180 }}
          options={options.map(({ value, label }) => ({ value, label }))}
          value={recordType}
          onChange={setSelected}
        />
      }
    >
      {error ? (
        <Text type="danger">Failed to load time in status.</Text>
      ) : (
        <Table<TimeInStatusReportRowDto>
          size="small"
          rowKey="statusId"
          columns={columns}
          dataSource={data?.statuses ?? []}
          loading={isFetching}
          pagination={false}
        />
      )}
    </Card>
  )
}

export default TimeInStatusReport
