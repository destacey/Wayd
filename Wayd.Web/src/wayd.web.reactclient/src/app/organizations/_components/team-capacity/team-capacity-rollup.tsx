'use client'

import Link from 'next/link'
import { Button, Flex } from 'antd'
import { CaretDownOutlined, CaretRightOutlined } from '@ant-design/icons'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import treeGridStyles from '@/src/components/common/wayd-grid/wayd-grid.module.css'
import { TeamCapacityRollupNodeDto } from '@/src/store/features/organization/team-capacity-api'
import {
  CapacityRange,
  formatPeriods,
  formatPersonDays,
  RollupRow,
  rollupRows,
} from './team-capacity-data'

const GRID_HEIGHT = 420

interface Props {
  /** The teams directly beneath the team of teams. */
  nodes: TeamCapacityRollupNodeDto[]
  range: CapacityRange
  isLoading: boolean
  onRefresh: () => void
}

/**
 * The teams beneath a team of teams as a tree, each with its capacity for the
 * days it was in the hierarchy and the people allocated to it. Read-only: a
 * team's allocations are changed on that team's page.
 */
const TeamCapacityRollup = ({ nodes, range, isLoading, onRefresh }: Props) => {
  const columns: ColumnDef<RollupRow, any>[] = [
    {
      id: 'name',
      accessorKey: 'name',
      header: 'Name',
      size: 300,
      // In tree mode the grid renders rows flat and leaves depth to the cell.
      cell: ({ row }) => (
        <Flex align="center" gap={0} className={treeGridStyles.nameCell}>
          {Array.from({ length: row.depth }).map((_, index) => (
            <span key={index} className={treeGridStyles.indentSpacer} />
          ))}
          {row.getCanExpand() ? (
            <Button
              type="text"
              size="small"
              aria-label={
                row.getIsExpanded()
                  ? `Collapse ${row.original.name}`
                  : `Expand ${row.original.name}`
              }
              icon={
                row.getIsExpanded() ? (
                  <CaretDownOutlined />
                ) : (
                  <CaretRightOutlined />
                )
              }
              onClick={row.getToggleExpandedHandler()}
              className={treeGridStyles.expanderBtn}
            />
          ) : (
            <span className={treeGridStyles.indentSpacer} />
          )}
          <Link href={row.original.href}>{row.original.name}</Link>
        </Flex>
      ),
    },
    {
      id: 'type',
      accessorKey: 'type',
      header: 'Type',
      size: 130,
      meta: { filterType: 'set' },
    },
    {
      id: 'periods',
      accessorFn: (row) =>
        row.periods ? formatPeriods(row.periods, range) : '',
      header: 'In hierarchy',
      size: 220,
    },
    {
      id: 'workingDays',
      accessorFn: (row) => row.workingDays ?? null,
      header: 'Working days',
      size: 120,
    },
    {
      id: 'allocated',
      accessorKey: 'allocatedPersonDays',
      header: 'Allocated',
      size: 110,
      cell: ({ row }) => formatPersonDays(row.original.allocatedPersonDays),
    },
    {
      id: 'delivery',
      accessorKey: 'deliveryPersonDays',
      header: 'Delivery',
      size: 110,
      cell: ({ row }) => formatPersonDays(row.original.deliveryPersonDays),
    },
  ]

  return (
    <WaydGrid<RollupRow>
      columns={columns}
      data={rollupRows(nodes)}
      getSubRows={(row) => row.children}
      getRowId={(row) => row.id}
      // Teams only at first: a planner opens the team they want to look into.
      initialExpanded={false}
      isLoading={isLoading}
      onRefresh={onRefresh}
      height={GRID_HEIGHT}
      emptyMessage="No teams were in this team of teams in this date range."
      persistStateKey="team-capacity-rollup"
      csvFileName="team-capacity-rollup"
    />
  )
}

export default TeamCapacityRollup
