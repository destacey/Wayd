'use client'

import { Col, Flex, Row, Table, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import dayjs from 'dayjs'
import { FC } from 'react'
import {
  AllocationDimension,
  AllocationGroupDto,
  TeamAllocationDto,
} from '@/src/services/wayd-api'
import WaydEmpty from '../../wayd-empty'
import { GroupName } from './allocation-breakdown'
import {
  GroupSwatch,
  formatAmount,
  formatShare,
  groupLabel,
} from './allocation-formatting'
import styles from './allocation-report.module.css'
import WaydTooltip from '../../wayd-tooltip'

const { Text } = Typography

export interface AllocationOverTimeProps {
  allocation: TeamAllocationDto
  swatches: GroupSwatch[]
  dimension: AllocationDimension
}

interface ChangeRow {
  group: AllocationGroupDto
  swatch: GroupSwatch
  first: number
  last: number
}

const periodLabel = (start: string | Date, end: string | Date) => {
  const from = dayjs(start)
  const to = dayjs(end)
  return from.isSame(to, 'month')
    ? `${from.format('MMM D')}–${to.format('D')}`
    : `${from.format('MMM D')}–${to.format('MMM D')}`
}

/** Change in percentage points, signed. */
const formatChange = (change: number) => {
  const rounded = Math.round(change)
  return rounded === 0
    ? '0 pts'
    : `${rounded > 0 ? '+' : '−'}${Math.abs(rounded)} pts`
}

const AllocationOverTime: FC<AllocationOverTimeProps> = ({
  allocation,
  swatches,
  dimension,
}) => {
  const periods = allocation.periods
  const worked = periods.filter((p) => p.value > 0)
  const byWorkType = dimension === AllocationDimension.WorkType

  if (worked.length === 0)
    return <WaydEmpty message="No completed work in this date range" />

  // Heights are each group's part of the credited total, which is its share except
  // when themes are counted in full and shares add up to more than 100%.
  const heights = periods.map((period) => {
    const credited = period.values.reduce((sum, value) => sum + value, 0)
    return period.values.map((value) =>
      credited > 0 ? (value / credited) * 100 : 0,
    )
  })

  const first = worked[0]
  const last = worked[worked.length - 1]
  const changeRows: ChangeRow[] = allocation.groups.map((group, index) => ({
    group,
    swatch: swatches[index],
    first: first.shares[index] ?? 0,
    last: last.shares[index] ?? 0,
  }))

  const changeColumns: ColumnsType<ChangeRow> = [
    {
      key: 'name',
      title: 'Group',
      render: (_, { group, swatch }) => (
        <GroupName group={group} swatch={swatch} dimension={dimension} />
      ),
    },
    {
      key: 'first',
      title: periodLabel(first.start, first.end),
      align: 'right',
      width: 110,
      render: (_, row) => formatShare(row.first),
    },
    {
      key: 'last',
      title: periodLabel(last.start, last.end),
      align: 'right',
      width: 110,
      render: (_, row) => formatShare(row.last),
    },
    {
      key: 'change',
      title: 'Change',
      align: 'right',
      width: 90,
      render: (_, row) => (
        <Text strong>{formatChange(row.last - row.first)}</Text>
      ),
    },
  ]

  return (
    <Row gutter={[24, 24]}>
      <Col xs={24} xl={15}>
        <Flex gap={12}>
          <div className={styles.axis} aria-hidden>
            <span>100%</span>
            <span>75%</span>
            <span>50%</span>
            <span>25%</span>
            <span>0%</span>
          </div>
          <div className={styles.columns}>
            {periods.map((period, p) => (
              <div key={`${period.start}`} className={styles.column}>
                <div
                  className={styles.columnStack}
                  role="img"
                  aria-label={`${periodLabel(period.start, period.end)}: ${allocation.groups
                    .map(
                      (g, i) =>
                        `${groupLabel(g, dimension)} ${formatShare(period.shares[i] ?? 0)}`,
                    )
                    .join(', ')}`}
                >
                  {allocation.groups.map((group, index) => {
                    const height = heights[p][index]
                    if (height <= 0) return null
                    return (
                      <WaydTooltip
                        key={group.id}
                        title={`${groupLabel(group, dimension)}: ${formatShare(period.shares[index] ?? 0)}`}
                      >
                        <div
                          className={styles.columnSegment}
                          style={{
                            height: `${height}%`,
                            background: swatches[index].background,
                            color: swatches[index].ink,
                          }}
                        >
                          {height >= 8
                            ? formatShare(period.shares[index] ?? 0)
                            : ''}
                        </div>
                      </WaydTooltip>
                    )
                  })}
                </div>
                <div className={styles.columnLabel}>
                  <Text strong style={{ fontSize: 12 }}>
                    {periodLabel(period.start, period.end)}
                  </Text>
                  <Text type="secondary" style={{ fontSize: 12 }}>
                    {`${formatAmount(period.items)} items`}
                  </Text>
                  {byWorkType && period.items > 0 && (
                    <Text type="warning" style={{ fontSize: 12 }}>
                      {`${formatShare(period.noProjectShare)} no project`}
                    </Text>
                  )}
                </div>
              </div>
            ))}
          </div>
        </Flex>
      </Col>
      <Col xs={24} xl={9}>
        <Table<ChangeRow>
          size="small"
          rowKey={({ group }) => group.id}
          columns={changeColumns}
          dataSource={changeRows}
          pagination={false}
          summary={() =>
            byWorkType ? (
              <Table.Summary.Row>
                <Table.Summary.Cell index={0}>
                  <Text type="warning">No project (any type)</Text>
                </Table.Summary.Cell>
                <Table.Summary.Cell index={1} align="right">
                  {formatShare(first.noProjectShare)}
                </Table.Summary.Cell>
                <Table.Summary.Cell index={2} align="right">
                  {formatShare(last.noProjectShare)}
                </Table.Summary.Cell>
                <Table.Summary.Cell index={3} align="right">
                  <Text strong>
                    {formatChange(last.noProjectShare - first.noProjectShare)}
                  </Text>
                </Table.Summary.Cell>
              </Table.Summary.Row>
            ) : undefined
          }
        />
      </Col>
    </Row>
  )
}

export default AllocationOverTime
