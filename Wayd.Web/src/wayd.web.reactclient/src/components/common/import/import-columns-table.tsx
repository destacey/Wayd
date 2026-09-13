'use client'

import { Flex, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import type { ImportColumn, ImportColumnType } from './import-template'

const { Text } = Typography

const TYPE_LABELS: Record<ImportColumnType, string> = {
  text: 'Text',
  id: 'Id',
  date: 'Date, YYYY-MM-DD',
  timestamp: 'Timestamp with offset',
  integer: 'Whole number',
  number: 'Number',
  boolean: 'true or false',
}

const accepts = (column: ImportColumn) => {
  if (column.values?.length) {
    return (
      <Flex wrap gap={4}>
        {column.values.map((value) => (
          <Tag key={value}>{value}</Tag>
        ))}
      </Flex>
    )
  }

  return column.type === 'text' && column.maxLength
    ? `Text, up to ${column.maxLength}`
    : TYPE_LABELS[column.type]
}

const tableColumns: ColumnsType<ImportColumn> = [
  {
    key: 'name',
    title: 'Column',
    render: (_, column) => (
      <Flex vertical gap={2}>
        <Text code>{column.name}</Text>
        {column.description && (
          <Text type="secondary" style={{ fontSize: 12 }}>
            {column.description}
          </Text>
        )}
      </Flex>
    ),
  },
  {
    key: 'required',
    title: 'Value',
    width: 100,
    render: (_, column) =>
      column.required ? <Tag color="blue">Required</Tag> : 'Optional',
  },
  {
    key: 'accepts',
    title: 'Accepts',
    width: 200,
    render: (_, column) => accepts(column),
  },
]

export interface ImportColumnsTableProps {
  columns: readonly ImportColumn[]
}

/** The columns a file must carry, in the order the template writes them. */
const ImportColumnsTable = ({ columns }: ImportColumnsTableProps) => (
  <Table<ImportColumn>
    size="small"
    rowKey="name"
    columns={tableColumns}
    dataSource={[...columns]}
    pagination={false}
    scroll={{ y: 280 }}
  />
)

export default ImportColumnsTable
