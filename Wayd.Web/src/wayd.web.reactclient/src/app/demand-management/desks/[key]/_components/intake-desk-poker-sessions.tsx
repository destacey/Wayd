'use client'

import EntityLink from '@/src/components/common/entity-link'
import { WaydGrid } from '@/src/components/common/wayd-grid'
import type { ColumnDef } from '@/src/components/common/wayd-grid-core'
import {
  IntakeDeskDto,
  PokerSessionHostKind,
  PokerSessionListDto,
} from '@/src/services/wayd-api'
import { useGetHostedPokerSessionsQuery } from '@/src/store/features/planning/poker-sessions-api'
import { Tag, Typography } from 'antd'
import { FC } from 'react'
import { pokerSessionHref } from '../../../_components/desk-poker'

const { Paragraph } = Typography

const columns: ColumnDef<PokerSessionListDto, any>[] = [
  {
    id: 'name',
    accessorKey: 'name',
    header: 'Session',
    size: 320,
    cell: ({ row }) => (
      <EntityLink href={pokerSessionHref(row.original.key)}>
        {row.original.name}
      </EntityLink>
    ),
  },
  {
    id: 'status',
    accessorKey: 'status',
    header: 'Status',
    size: 130,
    cell: ({ row }) => (
      <Tag color={row.original.status === 'Active' ? 'processing' : 'success'}>
        {row.original.status}
      </Tag>
    ),
  },
  { id: 'roundCount', accessorKey: 'roundCount', header: 'Rounds', size: 100 },
  {
    id: 'participants',
    accessorFn: (session) => session.participants.length,
    header: 'Participants',
    size: 130,
  },
]

export interface IntakeDeskPokerSessionsProps {
  desk: IntakeDeskDto
}

/**
 * The planning poker sessions the desk hosts, newest first, as the reader can
 * see them: the desk's agents see every one, and anyone else those they were
 * invited to. They are listed here and never in the main poker list.
 */
const IntakeDeskPokerSessions: FC<IntakeDeskPokerSessionsProps> = ({
  desk,
}) => {
  const { data, isLoading, refetch } = useGetHostedPokerSessionsQuery({
    hostKind: PokerSessionHostKind.IntakeDesk,
    hostId: desk.id,
  })

  return (
    <>
      <Paragraph type="secondary">
        Sessions where the desk&apos;s requests are sized with the people who
        can size them. Every agent on the desk runs them and invites the voters.
        A request&apos;s estimate changes only when a round is published.
      </Paragraph>
      <WaydGrid
        variant="simple"
        columns={columns}
        data={data}
        isLoading={isLoading}
        onRefresh={refetch}
        emptyMessage="No poker sessions on this desk"
      />
    </>
  )
}

export default IntakeDeskPokerSessions
