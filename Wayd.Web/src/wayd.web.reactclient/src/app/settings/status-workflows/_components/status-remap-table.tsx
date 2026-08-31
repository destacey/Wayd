'use client'

import {
  StatusRemapEntryDto,
  WorkflowStatusDto,
} from '@/src/services/wayd-api'
import { Alert, Flex, Select, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'

const { Text } = Typography

export interface StatusRemapTableProps {
  entries: StatusRemapEntryDto[]

  /** The statuses records can be sent to. */
  targetStatuses: WorkflowStatusDto[]

  /** Chosen target keyed by source status id. */
  decisions: Record<string, string>

  onChange: (fromStatusId: string, toStatusId: string) => void
}

/**
 * Every target stays editable, including auto-mapped ones: a category match is a
 * lone-candidate guess, and the domain permits overriding it.
 */
const StatusRemapTable = ({
  entries,
  targetStatuses,
  decisions,
  onChange,
}: StatusRemapTableProps) => {
  // Unresolved first — they are the only rows that require a person.
  const ordered = [...entries].sort((a, b) => {
    const aUnresolved = !decisions[a.from.id] ? 0 : 1
    const bUnresolved = !decisions[b.from.id] ? 0 : 1

    if (aUnresolved !== bUnresolved) return aUnresolved - bUnresolved

    return a.from.order - b.from.order
  })

  const unresolvedCount = entries.filter((e) => !decisions[e.from.id]).length

  const columns: ColumnsType<StatusRemapEntryDto> = [
    {
      title: 'From',
      key: 'from',
      render: (_, entry) => (
        <Flex vertical gap={2}>
          <Text>{entry.from.name}</Text>
          <Text type="secondary" style={{ fontSize: 12 }}>
            {entry.from.category?.name}
            {entry.from.aliasName ? ` · ${entry.from.aliasName}` : ''}
          </Text>
        </Flex>
      ),
    },
    {
      title: 'Records',
      key: 'records',
      width: 100,
      align: 'right',
      render: (_, entry) => (
        <Text type={entry.recordCount === 0 ? 'secondary' : undefined}>
          {entry.recordCount.toLocaleString()}
        </Text>
      ),
    },
    {
      title: 'To',
      key: 'to',
      width: 260,
      render: (_, entry) => (
        <Select
          value={decisions[entry.from.id]}
          onChange={(value) => onChange(entry.from.id, value)}
          placeholder="Choose a status"
          status={decisions[entry.from.id] ? undefined : 'error'}
          style={{ width: '100%' }}
          options={targetStatuses.map((status) => ({
            value: status.id,
            label: status.name,
          }))}
        />
      ),
    },
    {
      title: 'Matched by',
      key: 'matchedBy',
      width: 130,
      render: (_, entry) => <MatchTag entry={entry} decisions={decisions} />,
    },
  ]

  return (
    <Flex vertical gap={12}>
      {unresolvedCount > 0 && (
        <Alert
          type="warning"
          showIcon
          message={`${unresolvedCount} status${unresolvedCount === 1 ? '' : 'es'} still need a target.`}
          description="Every status must be mapped before records can move."
        />
      )}
      <Table
        size="small"
        rowKey={(entry) => entry.from.id}
        dataSource={ordered}
        columns={columns}
        pagination={false}
      />
    </Flex>
  )
}

const MatchTag = ({
  entry,
  decisions,
}: {
  entry: StatusRemapEntryDto
  decisions: Record<string, string>
}) => {
  if (!decisions[entry.from.id]) {
    return <Tag color="error">Unresolved</Tag>
  }

  // An override makes the original match irrelevant.
  if (decisions[entry.from.id] !== entry.to?.id) {
    return <Tag color="processing">Chosen</Tag>
  }

  switch (entry.matchedBy) {
    case 'Alias':
      return <Tag color="success">Alias</Tag>
    case 'Name':
      return <Tag color="blue">Name</Tag>
    case 'Category':
      return <Tag color="warning">Category</Tag>
    default:
      return <Tag color="error">Unresolved</Tag>
  }
}

export default StatusRemapTable
