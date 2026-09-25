'use client'

import { Alert, Flex, Table, Typography } from 'antd'
import type { ColumnsType } from 'antd/es/table'
import Link from 'next/link'
import { FC } from 'react'
import {
  AllocationDimension,
  AllocationGroupDto,
  AllocationMeasure,
  TeamAllocationDto,
} from '@/src/services/wayd-api'
import {
  GroupSwatch,
  describeGroup,
  formatAmount,
  formatShare,
  groupHref,
  groupLabel,
  hatch,
} from './allocation-formatting'
import styles from './allocation-report.module.css'
import WaydTooltip from '../../wayd-tooltip'

const { Text } = Typography

export interface AllocationBreakdownProps {
  allocation: TeamAllocationDto
  swatches: GroupSwatch[]
  dimension: AllocationDimension
  measure: AllocationMeasure
  /** Shares add up to more than 100%, so they cannot be drawn as one bar. */
  overlapping: boolean
  /** Names the largest contributing team; pointless when there is only one team. */
  showContributor: boolean
}

interface Row {
  group: AllocationGroupDto
  swatch: GroupSwatch
}

export const GroupName: FC<{
  group: AllocationGroupDto
  swatch: GroupSwatch
  dimension: AllocationDimension
}> = ({ group, swatch, dimension }) => {
  const href = groupHref(group, dimension)
  const label = groupLabel(group, dimension)
  const description = describeGroup(group, dimension)

  return (
    <Flex gap={10} align="center" style={{ minWidth: 0 }}>
      <span
        className={styles.swatch}
        style={{ background: swatch.background }}
      />
      <Flex vertical style={{ minWidth: 0 }}>
        <Text strong ellipsis={{ tooltip: label }}>
          {href ? <Link href={href}>{label}</Link> : label}
        </Text>
        {description && (
          <Text
            type="secondary"
            ellipsis={{ tooltip: description }}
            style={{ fontSize: 12 }}
          >
            {description}
          </Text>
        )}
      </Flex>
    </Flex>
  )
}

const AllocationBreakdown: FC<AllocationBreakdownProps> = ({
  allocation,
  swatches,
  dimension,
  measure,
  overlapping,
  showContributor,
}) => {
  const rows: Row[] = allocation.groups.map((group, index) => ({
    group,
    swatch: swatches[index],
  }))
  const largestShare = Math.max(...allocation.groups.map((g) => g.share), 1)
  const byWorkType = dimension === AllocationDimension.WorkType

  const columns: ColumnsType<Row> = [
    {
      key: 'name',
      title: dimensionTitle(dimension),
      render: (_, { group, swatch }) => (
        <GroupName group={group} swatch={swatch} dimension={dimension} />
      ),
    },
    {
      key: 'items',
      title: 'Count',
      align: 'right',
      width: 90,
      render: (_, { group }) => formatAmount(group.items),
    },
    {
      key: 'points',
      title: 'Story Points',
      align: 'right',
      width: 120,
      render: (_, { group }) => (
        <Flex vertical align="end">
          <span>
            {formatAmount(group.storyPoints + group.filledStoryPoints)}
          </span>
          {group.filledStoryPoints > 0 && (
            <Text type="warning" style={{ fontSize: 12 }}>
              {`+${formatAmount(group.filledStoryPoints)} filled in`}
            </Text>
          )}
        </Flex>
      ),
    },
    {
      key: 'share',
      title: measure === AllocationMeasure.TeamEffort ? 'Effort' : 'Share',
      align: 'right',
      width: 90,
      render: (_, { group }) => <Text strong>{formatShare(group.share)}</Text>,
    },
    {
      key: 'meter',
      width: 180,
      render: (_, { group, swatch }) => (
        <div className={styles.meter}>
          <div
            className={styles.meterFill}
            style={{
              width: `${(group.share / largestShare) * 100}%`,
              background: swatch.background,
            }}
          />
        </div>
      ),
    },
    ...(byWorkType
      ? [
          {
            key: 'noProject',
            title: 'No Project',
            align: 'right' as const,
            width: 120,
            render: (_: unknown, { group }: Row) =>
              group.noProjectShare != null && (
                <Text
                  type={group.noProjectShare >= 30 ? 'warning' : undefined}
                  strong={group.noProjectShare >= 30}
                >
                  {formatShare(group.noProjectShare)}
                </Text>
              ),
          },
        ]
      : []),
    ...(showContributor
      ? [
          {
            key: 'contributor',
            title: 'Largest Contributor',
            width: 240,
            render: (_: unknown, { group }: Row) =>
              group.largestContributor && (
                <Text type="secondary">
                  {`${group.largestContributor.name} · ${formatAmount(group.largestContributor.items)} of ${formatAmount(group.items)} items`}
                </Text>
              ),
          },
        ]
      : []),
  ]

  return (
    <Flex vertical gap="middle">
      {overlapping ? (
        <Alert
          type="info"
          showIcon
          title="Projects with several themes count in full under each, so shares add up to more than 100%."
        />
      ) : (
        <Flex vertical gap="small">
          <div
            className={styles.shareBar}
            role="img"
            aria-label={shareBarLabel(rows, dimension)}
          >
            {rows
              .filter(({ group }) => group.share > 0)
              .map(({ group, swatch }) => {
                const noProject = group.noProjectShare ?? 0
                return (
                  <WaydTooltip
                    key={group.id}
                    title={`${groupLabel(group, dimension)}: ${formatShare(group.share)}${
                      byWorkType
                        ? `, ${formatShare(noProject)} of it with no project`
                        : ''
                    }`}
                  >
                    <div
                      className={styles.shareSegment}
                      style={{ width: `${group.share}%` }}
                    >
                      <div
                        className={styles.shareSegmentLabel}
                        style={{
                          width: `${100 - noProject}%`,
                          background: swatch.background,
                          color: swatch.ink,
                        }}
                      >
                        {group.share >= 8
                          ? `${formatShare(group.share)} ${groupLabel(group, dimension)}`
                          : group.share >= 4
                            ? formatShare(group.share)
                            : ''}
                      </div>
                      {noProject > 0 && (
                        <div
                          style={{
                            width: `${noProject}%`,
                            background: hatch(swatch.background, 'transparent'),
                          }}
                        />
                      )}
                    </div>
                  </WaydTooltip>
                )
              })}
          </div>
          {byWorkType && (
            <Text type="secondary" style={{ fontSize: 12 }}>
              The hatched part of each type is work with no project.
            </Text>
          )}
        </Flex>
      )}

      <Table<Row>
        size="small"
        rowKey={({ group }) => group.id}
        columns={columns}
        dataSource={rows}
        pagination={false}
        scroll={{ x: 'max-content' }}
      />
    </Flex>
  )
}

export const dimensionTitle = (dimension: AllocationDimension) =>
  ({
    [AllocationDimension.Portfolio]: 'Portfolio',
    [AllocationDimension.Program]: 'Program',
    [AllocationDimension.Project]: 'Project',
    [AllocationDimension.StrategicTheme]: 'Strategic Theme',
    [AllocationDimension.WorkType]: 'Work Type',
  })[dimension]

const shareBarLabel = (rows: Row[], dimension: AllocationDimension) =>
  rows
    .map(
      ({ group }) =>
        `${groupLabel(group, dimension)} ${formatShare(group.share)}`,
    )
    .join(', ')

export default AllocationBreakdown
