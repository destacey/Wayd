'use client'

import { Flex, Table, Tag, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import { FC } from 'react'
import {
  WorkDistributionCellDto,
  WorkDistributionDimension,
  WorkDistributionGroupKind,
  WorkDistributionMeasure,
  WorkDistributionTeamRowDto,
  TeamWorkDistributionDto,
} from '@/src/services/wayd-api'
import {
  formatAmount,
  formatShare,
  groupLabel,
} from './work-distribution-formatting'
import styles from './work-distribution-report.module.css'
import WaydTooltip from '../../wayd-tooltip'

const { Text } = Typography

export interface WorkDistributionTeamMatrixProps {
  distribution: TeamWorkDistributionDto
  dimension: WorkDistributionDimension
  measure: WorkDistributionMeasure
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

const WorkDistributionTeamMatrix: FC<WorkDistributionTeamMatrixProps> = ({
  distribution,
  dimension,
  measure,
}) => {
  const byWorkType = dimension === WorkDistributionDimension.WorkType
  const unit =
    measure === WorkDistributionMeasure.StoryPoints
      ? 'story points'
      : measure === WorkDistributionMeasure.TeamEffort
        ? 'effort'
        : 'items'
  // Effort values are shares of the whole report, which mean nothing beside a row's own share.
  const amountOf = (cell: WorkDistributionCellDto) =>
    measure === WorkDistributionMeasure.StoryPoints ? cell.value : cell.items

  const columns: ColumnsType<WorkDistributionTeamRowDto> = [
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
    ...distribution.groups.map((group, index) => ({
      key: group.id,
      // Project names make the header rows several lines deep, so a project column
      // shows its key and names the project on hover.
      title:
        dimension === WorkDistributionDimension.Project && group.recordKey ? (
          <WaydTooltip title={groupLabel(group, dimension)}>
            <span>{group.recordKey}</span>
          </WaydTooltip>
        ) : (
          groupLabel(group, dimension)
        ),
      width: 130,
      onCell: () => ({ style: { padding: 3 } }),
      render: (_: unknown, row: WorkDistributionTeamRowDto) => {
        const cell = row.cells[index]
        if (row.excluded || row.value === 0 || !cell) return null
        return (
          <HeatCell
            share={cell.share}
            amount={amountOf(cell)}
            gap={group.kind === WorkDistributionGroupKind.NoProject}
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
            render: (_: unknown, row: WorkDistributionTeamRowDto) =>
              !row.excluded &&
              row.value > 0 &&
              row.noProjectShare != null && (
                <HeatCell
                  share={row.noProjectShare}
                  amount={
                    (row.noProjectShare / 100) *
                    (measure === WorkDistributionMeasure.StoryPoints
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
      <Table<WorkDistributionTeamRowDto>
        size="small"
        rowKey="teamId"
        columns={columns}
        dataSource={distribution.teams}
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

export default WorkDistributionTeamMatrix
