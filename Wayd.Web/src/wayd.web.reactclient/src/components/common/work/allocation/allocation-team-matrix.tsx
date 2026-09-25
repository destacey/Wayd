'use client'

import { Flex, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { FC } from 'react'
import {
  AllocationCellDto,
  AllocationDimension,
  AllocationGroupKind,
  AllocationMeasure,
  AllocationTeamRowDto,
  TeamAllocationDto,
} from '@/src/services/wayd-api'
import { formatAmount, formatShare, groupLabel } from './allocation-formatting'
import styles from './allocation-report.module.css'
import WaydTooltip from '../../wayd-tooltip'

const { Text } = Typography

export interface AllocationTeamMatrixProps {
  allocation: TeamAllocationDto
  dimension: AllocationDimension
  measure: AllocationMeasure
}

const HEAT_STEPS = [10, 25, 40, 55]
const HEAT_LABELS = ['Under 10%', '10–25%', '25–40%', '40–55%', '55% and over']

const heatStep = (share: number) => {
  const step = HEAT_STEPS.findIndex((limit) => share < limit)
  return step === -1 ? HEAT_STEPS.length : step
}

const HeatCell: FC<{
  share: number
  amount: number
  gap: boolean
  title: string
}> = ({ share, amount, gap, title }) => {
  const step = heatStep(share)
  return (
    <WaydTooltip title={title}>
      <div
        className={`${styles.heatCell} ${styles[`${gap ? 'gap' : 'heat'}${step}`]}`}
      >
        <span className={styles.heatValue}>{formatShare(share)}</span>
        <span className={styles.heatItems}>{formatAmount(amount)}</span>
      </div>
    </WaydTooltip>
  )
}

const AllocationTeamMatrix: FC<AllocationTeamMatrixProps> = ({
  allocation,
  dimension,
  measure,
}) => {
  const byWorkType = dimension === AllocationDimension.WorkType
  const unit =
    measure === AllocationMeasure.StoryPoints
      ? 'story points'
      : measure === AllocationMeasure.TeamEffort
        ? 'effort'
        : 'items'
  // Effort values are shares of the whole report, which mean nothing beside a row's own share.
  const amountOf = (cell: AllocationCellDto) =>
    measure === AllocationMeasure.StoryPoints ? cell.value : cell.items

  const columns: ColumnsType<AllocationTeamRowDto> = [
    {
      key: 'team',
      title: 'Team',
      fixed: 'left',
      width: 280,
      render: (_, row) => (
        <Flex
          gap={8}
          align="center"
          style={{ paddingInlineStart: row.level * 20 }}
        >
          <Text strong={row.isTeamOfTeams}>{row.name}</Text>
          {row.isTeamOfTeams && <Tag color="blue">Team of Teams</Tag>}
          {row.excluded && (
            <WaydTooltip title={row.excludedReason} helpCursor>
              <Tag>Excluded</Tag>
            </WaydTooltip>
          )}
        </Flex>
      ),
    },
    ...allocation.groups.map((group, index) => ({
      key: group.id,
      title: groupLabel(group, dimension),
      width: 130,
      onCell: () => ({ style: { padding: 3 } }),
      render: (_: unknown, row: AllocationTeamRowDto) => {
        const cell = row.cells[index]
        if (row.excluded || row.value === 0 || !cell) return null
        return (
          <HeatCell
            share={cell.share}
            amount={amountOf(cell)}
            gap={group.kind === AllocationGroupKind.NoProject}
            title={`${row.name}: ${formatShare(cell.share)} of its ${unit} in ${groupLabel(group, dimension)}`}
          />
        )
      },
    })),
    ...(byWorkType
      ? [
          {
            key: 'noProject',
            title: 'No Project (any type)',
            width: 130,
            onCell: () => ({ style: { padding: 3 } }),
            render: (_: unknown, row: AllocationTeamRowDto) =>
              !row.excluded &&
              row.value > 0 &&
              row.noProjectShare != null && (
                <HeatCell
                  share={row.noProjectShare}
                  amount={
                    (row.noProjectShare / 100) *
                    (measure === AllocationMeasure.StoryPoints
                      ? row.value
                      : row.items)
                  }
                  gap
                  title={`${row.name}: ${formatShare(row.noProjectShare)} of its ${unit} had no project`}
                />
              ),
          },
        ]
      : []),
    {
      key: 'items',
      title: 'Count',
      align: 'right',
      width: 80,
      render: (_, row) => (
        <Text strong={row.isTeamOfTeams}>{formatAmount(row.items)}</Text>
      ),
    },
    {
      key: 'points',
      title: 'Story Points',
      align: 'right',
      width: 110,
      render: (_, row) =>
        row.excluded ? (
          <Text type="secondary">—</Text>
        ) : (
          formatAmount(row.storyPoints)
        ),
    },
  ]

  return (
    <Flex vertical gap="small">
      <Table<AllocationTeamRowDto>
        size="small"
        rowKey="teamId"
        columns={columns}
        dataSource={allocation.teams}
        pagination={false}
        scroll={{ x: 'max-content' }}
      />
      <Flex gap="middle" wrap align="center">
        <Text type="secondary" style={{ fontSize: 12 }}>
          {`Share of each team's ${unit}`}
        </Text>
        {HEAT_LABELS.map((label, step) => (
          <Flex key={label} gap={6} align="center">
            <span className={`${styles.swatch} ${styles[`heat${step}`]}`} />
            <Text type="secondary" style={{ fontSize: 12 }}>
              {label}
            </Text>
          </Flex>
        ))}
      </Flex>
    </Flex>
  )
}

export default AllocationTeamMatrix
