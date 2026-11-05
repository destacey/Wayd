'use client'

import { DependencyAnalysisPairDto } from '@/src/services/wayd-api'
import { Tag, Typography } from 'antd'
import { FC } from 'react'
import { WaydGrid, renderTeamLink } from '../../wayd-grid'
import type { ColumnDef } from '../../wayd-grid-core'

const { Text } = Typography

export interface DependencyAnalysisPairsProps {
  pairs: DependencyAnalysisPairDto[]
  /** Marks the teams outside a team of teams; for one team, every partner is outside, so nothing is marked. */
  markOutside: boolean
}

type PairTeam = DependencyAnalysisPairDto['predecessorTeam']

const pairId = (pair: DependencyAnalysisPairDto) =>
  `${pair.predecessorTeam?.id ?? 'none'}:${pair.successorTeam?.id ?? 'none'}`

/**
 * Each predecessor team → successor team pair in the range, most dependencies first: who the teams wait on,
 * and how many of those dependencies were created, closed, and still open at the end.
 */
const DependencyAnalysisPairs: FC<DependencyAnalysisPairsProps> = ({
  pairs,
  markOutside,
}) => {
  const renderTeam = (team: PairTeam, inScope: boolean) =>
    team ? (
      <>
        {renderTeamLink(team)}
        {markOutside && !inScope && (
          <Tag style={{ marginInlineStart: 8 }}>Outside</Tag>
        )}
      </>
    ) : (
      <Text type="secondary">No team</Text>
    )

  const countColumn = (
    id: 'dependencies' | 'created' | 'closed' | 'openAtEnd',
    header: string,
  ): ColumnDef<DependencyAnalysisPairDto> => ({
    id,
    accessorKey: id,
    header,
    size: 130,
    meta: { filterType: 'number' },
  })

  const columns: ColumnDef<DependencyAnalysisPairDto>[] = [
    {
      id: 'predecessorTeam',
      accessorFn: (pair) => pair.predecessorTeam?.name ?? 'No team',
      header: 'Predecessor Team',
      size: 220,
      cell: ({ row }) =>
        renderTeam(
          row.original.predecessorTeam,
          row.original.predecessorTeamInScope,
        ),
    },
    {
      id: 'successorTeam',
      accessorFn: (pair) => pair.successorTeam?.name ?? 'No team',
      header: 'Successor Team',
      size: 220,
      cell: ({ row }) =>
        renderTeam(
          row.original.successorTeam,
          row.original.successorTeamInScope,
        ),
    },
    countColumn('dependencies', 'Dependencies'),
    countColumn('created', 'Created'),
    countColumn('closed', 'Closed'),
    countColumn('openAtEnd', 'Open at End'),
  ]

  return (
    <WaydGrid
      variant="simple"
      columns={columns}
      data={pairs}
      getRowId={pairId}
      includeIdColumn={false}
    />
  )
}

export default DependencyAnalysisPairs
